using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Constants;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles <see cref="ContactGdprDeletedIntegrationEvent"/> from the Contacts module.
/// Performs GDPR-compliant cleanup inside the Documents module:
/// (1) unlinks Documents whose <c>LinkedEntityType = "Contact"</c> and
/// <c>LinkedEntityId = ContactId</c> — Documents themselves are retained, but the
/// reference to the contact is removed;
/// (2) scrubs PII (Name, Email, IpAddress) on signature recipients bound to the contact.
/// Idempotent via <see cref="IInboxGuard"/> (ADR-0011 inbox pattern).
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandler(
    DocumentsDbContext dbContext,
    IInboxGuard inboxGuard,
    ILogger<ContactGdprDeletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>
{
    private const string LinkedEntityTypeContact = "Contact";

    /// <summary>
    /// Handles a <see cref="ContactGdprDeletedIntegrationEvent"/> by unlinking matching
    /// Documents and scrubbing signature recipient PII tied to the contact.
    /// </summary>
    public async Task HandleAsync(ContactGdprDeletedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug(
                "Skipping duplicate event {EventId} of type {EventType}",
                @event.EventId, @event.GetType().Name);
            return;
        }

        logger.LogInformation(
            "Processing ContactGdprDeletedIntegrationEvent for TenantId {TenantId}, ContactId {ContactId}, Mode {Mode}",
            @event.TenantId, @event.ContactId, @event.Mode);

        if (!Guid.TryParse(@event.TenantId, out var tenantId))
        {
            logger.LogWarning(
                "GDPR erasure event {EventId} has invalid TenantId {TenantId}; marking processed to prevent redelivery loop",
                @event.EventId, @event.TenantId);
            inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        int unlinkedCount;
        int scrubbedCount;

        if (dbContext.Database.IsRelational())
        {
            // (1) Unlink Documents referencing this contact — keep the document, drop the link.
            // Scoped to the event's tenant to avoid cross-tenant writes if a Document row
            // was produced with a mismatched LinkedEntityId.
            unlinkedCount = await dbContext.Documents
                .Where(d => d.TenantId == tenantId
                         && d.LinkedEntityType == LinkedEntityTypeContact
                         && d.LinkedEntityId == @event.ContactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.LinkedEntityId, (Guid?)null)
                    .SetProperty(d => d.LinkedEntityType, (string?)null),
                    ct);

            // (2) Scrub PII on signature recipients whose ContactId matches — we keep the
            // signing audit trail (status, timestamps, signature data) but remove direct
            // personal identifiers (name, email, IP).
            scrubbedCount = await dbContext.SignatureRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Name, PiiRedactedPlaceholder.Value)
                    .SetProperty(r => r.Email, PiiRedactedPlaceholder.Value)
                    .SetProperty(r => r.IpAddress, (string?)null),
                    ct);
        }
        else
        {
            // InMemory fallback — ExecuteUpdateAsync is unsupported on the in-memory provider
            // used by tests. Materialize and invoke domain methods to preserve behaviour.
            var linkedDocuments = await dbContext.Documents
                .Where(d => d.TenantId == tenantId
                         && d.LinkedEntityType == LinkedEntityTypeContact
                         && d.LinkedEntityId == @event.ContactId)
                .ToListAsync(ct);

            foreach (var document in linkedDocuments)
                document.UnlinkEntity();

            var recipients = await dbContext.SignatureRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ToListAsync(ct);

            foreach (var recipient in recipients)
                recipient.ScrubPii();

            unlinkedCount = linkedDocuments.Count;
            scrubbedCount = recipients.Count;
        }

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Completed ContactGdprDeletedIntegrationEvent for TenantId {TenantId}, ContactId {ContactId}: " +
            "UnlinkedDocuments {UnlinkedCount}, ScrubbedRecipients {ScrubbedCount}",
            @event.TenantId, @event.ContactId, unlinkedCount, scrubbedCount);
    }
}
