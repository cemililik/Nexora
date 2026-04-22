using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Audit.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles <see cref="ContactGdprDeletedIntegrationEvent"/> from the Contacts module.
/// Preserves audit trail integrity by redacting PII from payload columns on existing
/// audit entries referencing the erased contact, and appending a new compliance
/// audit record that captures the erasure itself.
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandler(
    AuditDbContext dbContext,
    IInboxGuard inboxGuard,
    ILogger<ContactGdprDeletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>
{
    private const string EntityTypeContact = "Contact";
    private const string ErasureOperation = "gdpr_erasure";
    private const string ErasureModule = "contacts";
    private const string ErasureOperationType = "Action";

    /// <inheritdoc />
    public async Task HandleAsync(ContactGdprDeletedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug(
                "Skipping duplicate event {EventId} of type {EventType}",
                @event.EventId, @event.GetType().Name);
            return;
        }

        var contactIdString = @event.ContactId.ToString();
        var redactionMarker = BuildRedactionMarker(@event.DeletedAtUtc, @event.ErasedByUserId);

        // Load all audit entries for this tenant that reference the erased contact.
        // Match by (EntityType = "Contact" AND EntityId = contactId) — this is the primary,
        // indexed path. Payload-scan matches are not reliable across free-form JSON columns.
        var matchingEntries = await dbContext.AuditEntries
            .Where(e => e.TenantId == @event.TenantId
                && e.EntityType == EntityTypeContact
                && e.EntityId == contactIdString)
            .ToListAsync(ct);

        foreach (var entry in matchingEntries)
        {
            entry.RedactPayloadForGdpr(redactionMarker);
        }

        // Append the compliance record of the erasure itself.
        var erasurePayload = JsonSerializer.Serialize(new
        {
            reason = @event.Reason,
            mode = @event.Mode
        });

        var erasureEntry = AuditEntry.Create(
            tenantId: @event.TenantId,
            module: ErasureModule,
            operation: ErasureOperation,
            operationType: ErasureOperationType,
            userId: @event.ErasedByUserId,
            userEmail: null,
            ipAddress: null,
            userAgent: null,
            correlationId: null,
            isSuccess: true,
            errorKey: null,
            entityType: EntityTypeContact,
            entityId: contactIdString,
            beforeState: null,
            afterState: erasurePayload,
            changes: null,
            metadata: null,
            timestamp: new DateTimeOffset(DateTime.SpecifyKind(@event.DeletedAtUtc, DateTimeKind.Utc)));

        dbContext.AuditEntries.Add(erasureEntry);
        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Redacted {RedactedCount} audit entries and appended gdpr_erasure record for contact {ContactId} in tenant {TenantId} (erased by {ErasedByUserId}, mode {Mode})",
            matchingEntries.Count, @event.ContactId, @event.TenantId, @event.ErasedByUserId, @event.Mode);
    }

    private static string BuildRedactionMarker(DateTime deletedAtUtc, Guid erasedByUserId)
    {
        var utcTimestamp = DateTime.SpecifyKind(deletedAtUtc, DateTimeKind.Utc);
        return JsonSerializer.Serialize(new
        {
            _redacted = true,
            _reason = "gdpr_erasure",
            _erasedAtUtc = utcTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            _erasedByUserId = erasedByUserId.ToString()
        });
    }
}
