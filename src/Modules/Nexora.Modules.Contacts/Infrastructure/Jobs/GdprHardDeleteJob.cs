using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Infrastructure.Jobs;

/// <summary>Parameters for the GDPR hard-delete job.</summary>
public sealed record GdprHardDeleteParams : JobParams
{
    /// <summary>Identifier of the contact to permanently erase.</summary>
    public required Guid ContactId { get; init; }

    /// <summary>Free-text reason supplied with the erasure request.</summary>
    public required string Reason { get; init; }

    /// <summary>User who initiated or approved the erasure.</summary>
    public required Guid ErasedByUserId { get; init; }
}

/// <summary>
/// Permanently erases all contact data for a tenant's contact in a single transaction.
/// FK-safe deletion order:
/// addresses → notes → custom fields → tags → relationships →
/// comm preferences → activities → (consents anonymized, not deleted) → contact.
/// Writes a <see cref="GdprErasureAudit"/> row and enqueues the
/// <see cref="ContactGdprDeletedIntegrationEvent"/> atomically via the outbox.
/// </summary>
public sealed class GdprHardDeleteJob(
    ITenantContextAccessor tenantContextAccessor,
    ContactsDbContext dbContext,
    IOutbox outbox,
    ILogger<GdprHardDeleteJob> logger)
    : NexoraJob<GdprHardDeleteParams>(tenantContextAccessor, logger)
{
    private const string RedactedPlaceholder = "REDACTED";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(GdprHardDeleteParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var contactId = ContactId.From(parameters.ContactId);

        var contact = await dbContext.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == contactId && c.TenantId == tenantId, ct);

        if (contact is null)
        {
            // Idempotent: job may have run before or been re-scheduled after completion.
            logger.LogWarning(
                "GDPR hard-delete skipped — contact {ContactId} not found (already erased?)",
                parameters.ContactId);
            return;
        }

        if (contact.Status == ContactStatus.Merged)
            throw new DomainException("lockey_contacts_error_gdpr_delete_merged_contact");

        var supportsTransactions = dbContext.Database.IsRelational();
        var transaction = supportsTransactions
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;

        // Disable the soft-delete interceptor for the duration of this operation —
        // GDPR Article 17 requires permanent erasure, not flagging.
        dbContext.IsHardDeleteModeEnabled = true;

        try
        {
            // Count first (ExecuteDeleteAsync returns affected rows but we need counts per type
            // BEFORE deletion for child counts JSON, and ExecuteDeleteAsync is safe on InMemory
            // only via fallback path — we use raw counts for compatibility).
            var addressCount = await dbContext.ContactAddresses
                .IgnoreQueryFilters()
                .CountAsync(a => a.ContactId == contactId, ct);
            var noteCount = await dbContext.ContactNotes
                .IgnoreQueryFilters()
                .CountAsync(n => n.ContactId == contactId, ct);
            var customFieldCount = await dbContext.ContactCustomFields
                .IgnoreQueryFilters()
                .CountAsync(f => f.ContactId == contactId, ct);
            var tagCount = await dbContext.ContactTags
                .IgnoreQueryFilters()
                .CountAsync(t => t.ContactId == contactId, ct);
            var relationshipCount = await dbContext.ContactRelationships
                .IgnoreQueryFilters()
                .CountAsync(r => r.ContactId == contactId || r.RelatedContactId == contactId, ct);
            var communicationPreferenceCount = await dbContext.CommunicationPreferences
                .IgnoreQueryFilters()
                .CountAsync(p => p.ContactId == contactId, ct);
            var activityCount = await dbContext.ContactActivities
                .IgnoreQueryFilters()
                .CountAsync(a => a.ContactId == contactId, ct);
            var consentCount = await dbContext.ConsentRecords
                .IgnoreQueryFilters()
                .CountAsync(c => c.ContactId == contactId, ct);

            // FK-safe order. Uses ExecuteDeleteAsync to bypass the soft-delete audit
            // interceptor and perform a true hard delete.
            await HardDeleteAsync(
                dbContext.ContactAddresses.IgnoreQueryFilters().Where(a => a.ContactId == contactId),
                a => dbContext.ContactAddresses.Remove(a),
                ct);
            await HardDeleteAsync(
                dbContext.ContactNotes.IgnoreQueryFilters().Where(n => n.ContactId == contactId),
                n => dbContext.ContactNotes.Remove(n),
                ct);
            await HardDeleteAsync(
                dbContext.ContactCustomFields.IgnoreQueryFilters().Where(f => f.ContactId == contactId),
                f => dbContext.ContactCustomFields.Remove(f),
                ct);
            await HardDeleteAsync(
                dbContext.ContactTags.IgnoreQueryFilters().Where(t => t.ContactId == contactId),
                t => dbContext.ContactTags.Remove(t),
                ct);
            await HardDeleteAsync(
                dbContext.ContactRelationships.IgnoreQueryFilters()
                    .Where(r => r.ContactId == contactId || r.RelatedContactId == contactId),
                r => dbContext.ContactRelationships.Remove(r),
                ct);
            await HardDeleteAsync(
                dbContext.CommunicationPreferences.IgnoreQueryFilters().Where(p => p.ContactId == contactId),
                p => dbContext.CommunicationPreferences.Remove(p),
                ct);
            await HardDeleteAsync(
                dbContext.ContactActivities.IgnoreQueryFilters().Where(a => a.ContactId == contactId),
                a => dbContext.ContactActivities.Remove(a),
                ct);

            // Consents: anonymize but keep the row (Article 17(3)(e) — legal claims defense).
            // Only IpAddress is PII on the current schema; Source is a free-text origin label
            // that we also redact defensively.
            await AnonymizeConsentsAsync(contactId, ct);

            // Finally delete the contact itself.
            await HardDeleteAsync(
                dbContext.Contacts.IgnoreQueryFilters().Where(c => c.Id == contactId),
                c => dbContext.Contacts.Remove(c),
                ct);

            var childCounts = new Dictionary<string, int>
            {
                ["addresses"] = addressCount,
                ["notes"] = noteCount,
                ["customFields"] = customFieldCount,
                ["tags"] = tagCount,
                ["relationships"] = relationshipCount,
                ["communicationPreferences"] = communicationPreferenceCount,
                ["activities"] = activityCount,
                ["consentsAnonymized"] = consentCount
            };
            var childCountsJson = JsonSerializer.Serialize(childCounts);

            var deletedAtUtc = DateTime.UtcNow;

            dbContext.GdprErasureAudits.Add(GdprErasureAudit.Create(
                tenantId: tenantId,
                contactId: parameters.ContactId,
                erasedByUserId: parameters.ErasedByUserId,
                reason: parameters.Reason,
                mode: "hard_deleted",
                childCountsJson: childCountsJson));

            await outbox.EnqueueAsync(new ContactGdprDeletedIntegrationEvent
            {
                TenantId = parameters.TenantId,
                ContactId = parameters.ContactId,
                Reason = parameters.Reason,
                Mode = "hard_deleted",
                DeletedAtUtc = deletedAtUtc,
                ErasedByUserId = parameters.ErasedByUserId
            }, ct);

            await dbContext.SaveChangesAsync(ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            logger.LogInformation(
                "GDPR hard-delete completed for contact {ContactId}, erased by {ErasedByUserId}, child counts {ChildCounts}",
                parameters.ContactId, parameters.ErasedByUserId, childCountsJson);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            dbContext.IsHardDeleteModeEnabled = false;
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// Deletes rows via <c>ExecuteDeleteAsync</c> on relational providers (bypasses the
    /// soft-delete interceptor) or falls back to change-tracker deletion on in-memory
    /// providers used by tests.
    /// </summary>
    private async Task HardDeleteAsync<TEntity>(
        IQueryable<TEntity> query,
        Action<TEntity> fallbackRemove,
        CancellationToken ct) where TEntity : class
    {
        if (dbContext.Database.IsRelational())
        {
            await query.ExecuteDeleteAsync(ct);
            return;
        }

        var rows = await query.ToListAsync(ct);
        foreach (var row in rows)
            fallbackRemove(row);
    }

    private async Task AnonymizeConsentsAsync(ContactId contactId, CancellationToken ct)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.ConsentRecords
                .IgnoreQueryFilters()
                .Where(c => c.ContactId == contactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.IpAddress, _ => null)
                    .SetProperty(c => c.Source, _ => RedactedPlaceholder), ct);
            return;
        }

        var consents = await dbContext.ConsentRecords
            .IgnoreQueryFilters()
            .Where(c => c.ContactId == contactId)
            .ToListAsync(ct);

        foreach (var consent in consents)
            consent.Anonymize(RedactedPlaceholder);
    }
}
