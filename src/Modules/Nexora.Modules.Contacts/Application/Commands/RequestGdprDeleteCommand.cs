using System.Text.Json;
using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Constants;
using Nexora.SharedKernel.Domain.Events;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to erase a contact's personal data (GDPR right to erasure).</summary>
public sealed record RequestGdprDeleteCommand(Guid ContactId, string Reason) : ICommand;

/// <summary>
/// Dispatches a GDPR erasure request. When the tenant feature flag
/// <c>gdpr.hard_delete.enabled</c> is <c>false</c> (default), the contact is anonymized
/// in-process. When <c>true</c>, a <see cref="GdprHardDeleteJob"/> is enqueued to perform
/// the permanent deletion in a single transaction.
/// </summary>
public sealed class RequestGdprDeleteHandler(
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ITenantConfiguration tenantConfiguration,
    IOutbox outbox,
    IBackgroundJobClient backgroundJobClient,
    ILogger<RequestGdprDeleteHandler> logger) : ICommandHandler<RequestGdprDeleteCommand>
{
    private const string HardDeleteFlagKey = "gdpr.hard_delete.enabled";
    private const int ErasureDebounceSeconds = 60;

    public async Task<Result> Handle(
        RequestGdprDeleteCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (!Guid.TryParse(tenantContextAccessor.Current.UserId, out var erasedByUserId)
            || erasedByUserId == Guid.Empty)
        {
            logger.LogWarning("GDPR delete rejected — no valid user context for actor");
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_invalid_user_context"));
        }

        // Debounce duplicate submissions — two rapid clicks / retries within a short window
        // should NOT produce two audit rows. Runs BEFORE the contact lookup so the second
        // call does not trip the "not found" branch once the contact has been soft-deleted
        // by the first call's anonymize path.
        var debounceThreshold = DateTime.UtcNow.AddSeconds(-ErasureDebounceSeconds);
        var recentErasureExists = await dbContext.GdprErasureAudits
            .AnyAsync(a => a.ContactId == request.ContactId
                        && a.TenantId == tenantId
                        && a.ErasedAtUtc >= debounceThreshold, cancellationToken);

        if (recentErasureExists)
        {
            logger.LogInformation(
                "GDPR erasure already processed within the last {DebounceSeconds}s for contact {ContactId}; request treated as duplicate.",
                ErasureDebounceSeconds, request.ContactId);
            return Result.Success(LocalizedMessage.Of("lockey_contacts_gdpr_erasure_already_processed"));
        }

        var contactId = ContactId.From(request.ContactId);

        var contact = await dbContext.Contacts.FirstOrDefaultAsync(
            c => c.Id == contactId && c.TenantId == tenantId,
            cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("GDPR delete requested for non-existent contact {ContactId}", request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_contact_not_found"));
        }

        if (contact.Status == ContactStatus.Merged)
        {
            logger.LogWarning("GDPR delete not allowed for merged contact {ContactId}", request.ContactId);
            return Result.Failure(LocalizedMessage.Of("lockey_contacts_error_gdpr_delete_merged_contact"));
        }

        var hardDeleteEnabled = await tenantConfiguration.GetAsync<bool>(HardDeleteFlagKey, cancellationToken);

        if (hardDeleteEnabled)
        {
            // Defer to the Hangfire job — the job performs the hard delete, writes the
            // audit row, and emits the outbox event in a single transaction.
            var hangfireJobId = backgroundJobClient.Enqueue<GdprHardDeleteJob>(j => j.RunAsync(
                new GdprHardDeleteParams
                {
                    TenantId = tenantId.ToString(),
                    OrganizationId = tenantContextAccessor.Current.OrganizationId,
                    ContactId = request.ContactId,
                    Reason = request.Reason,
                    ErasedByUserId = erasedByUserId
                },
                CancellationToken.None));

            logger.LogInformation(
                "GDPR hard-delete job {HangfireJobId} enqueued for contact {ContactId} by user {ErasedByUserId}",
                hangfireJobId, request.ContactId, erasedByUserId);

            return Result.Success(LocalizedMessage.Of("lockey_contacts_gdpr_erasure_enqueued"));
        }

        // Anonymize path (default): PII blanked, child PII rows removed, event emitted.
        contact.Update(
            firstName: PiiRedactedPlaceholder.Value,
            lastName: PiiRedactedPlaceholder.Value,
            companyName: null,
            email: null,
            phone: null,
            mobile: null,
            website: null,
            taxId: null,
            language: contact.Language,
            currency: contact.Currency,
            title: null);

        // Revoke all active consents (Article 17(3)(e) — keep row, strip grant).
        var activeConsents = await dbContext.ConsentRecords
            .Where(c => c.ContactId == contactId && c.Granted && c.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var consent in activeConsents)
            consent.Revoke();

        var addresses = await dbContext.ContactAddresses
            .Where(a => a.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactAddresses.RemoveRange(addresses);

        var notes = await dbContext.ContactNotes
            .Where(n => n.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactNotes.RemoveRange(notes);

        var customFields = await dbContext.ContactCustomFields
            .Where(f => f.ContactId == contactId)
            .ToListAsync(cancellationToken);
        dbContext.ContactCustomFields.RemoveRange(customFields);

        // Both paths (anonymize and hard_deleted) write the same 8 keys for forensic
        // consistency. On the anonymize path, rows NOT modified are reported as 0 —
        // preserved entities (tags, relationships, communicationPreferences, activities)
        // stay in the tenant schema because they do not carry standalone PII.
        var childCounts = new Dictionary<string, int>
        {
            ["addresses"] = addresses.Count,
            ["notes"] = notes.Count,
            ["customFields"] = customFields.Count,
            ["tags"] = 0,
            ["relationships"] = 0,
            ["communicationPreferences"] = 0,
            ["activities"] = 0,
            ["consentsAnonymized"] = activeConsents.Count
        };
        var childCountsJson = JsonSerializer.Serialize(childCounts);

        var deletedAtUtc = DateTime.UtcNow;

        dbContext.GdprErasureAudits.Add(GdprErasureAudit.Create(
            tenantId: tenantId,
            contactId: request.ContactId,
            erasedByUserId: erasedByUserId,
            reason: request.Reason,
            mode: "anonymized",
            childCountsJson: childCountsJson));

        await outbox.EnqueueAsync(new ContactGdprDeletedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = request.ContactId,
            Reason = request.Reason,
            Mode = "anonymized",
            DeletedAtUtc = deletedAtUtc,
            ErasedByUserId = erasedByUserId
        }, cancellationToken);

        // Soft-delete the contact (BaseDbContext intercepts Remove and sets IsDeleted=true)
        dbContext.Contacts.Remove(contact);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "GDPR anonymize completed for contact {ContactId} by user {ErasedByUserId}. Reason: {Reason}",
            contactId.Value, erasedByUserId, request.Reason);

        return Result.Success(LocalizedMessage.Of("lockey_contacts_gdpr_delete_completed"));
    }
}

/// <summary>Validates GDPR delete request input.</summary>
public sealed class RequestGdprDeleteCommandValidator : AbstractValidator<RequestGdprDeleteCommand>
{
    public RequestGdprDeleteCommandValidator()
    {
        RuleFor(x => x.ContactId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("lockey_validation_max_length");
    }
}
