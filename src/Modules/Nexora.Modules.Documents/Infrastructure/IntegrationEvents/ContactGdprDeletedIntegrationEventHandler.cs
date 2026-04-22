using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
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

        // (1) Unlink Documents referencing this contact — keep the document, drop the link.
        var linkedDocuments = await dbContext.Documents
            .Where(d => d.LinkedEntityType == LinkedEntityTypeContact
                     && d.LinkedEntityId == @event.ContactId)
            .ToListAsync(ct);

        foreach (var document in linkedDocuments)
            document.UnlinkEntity();

        // (2) Scrub PII on signature recipients whose ContactId matches — we keep the
        // signing audit trail (status, timestamps, signature data) but remove direct
        // personal identifiers (name, email, IP).
        var recipients = await dbContext.SignatureRecipients
            .Where(r => r.ContactId == @event.ContactId)
            .ToListAsync(ct);

        foreach (var recipient in recipients)
            recipient.ScrubPii();

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Completed ContactGdprDeletedIntegrationEvent for TenantId {TenantId}, ContactId {ContactId}: " +
            "UnlinkedDocuments {UnlinkedCount}, ScrubbedRecipients {ScrubbedCount}",
            @event.TenantId, @event.ContactId, linkedDocuments.Count, recipients.Count);
    }
}
