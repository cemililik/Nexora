using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Gdpr;
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
    IGdprRenamedTableScanner<DocumentsDbContext> renamedTableScanner,
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

        // T-027: GDPR escape hatch — apply the same scrub to any
        // documents_*_del_* tables left behind by a previous module
        // uninstall (ADR-0028 retention window). Two PII-bearing tables
        // exist:
        //   - documents_signature_recipients_*: Name/Email/IpAddress
        //   - documents_documents_*: LinkedEntityId/LinkedEntityType
        //     (drop the back-reference to the contact)
        // Note the canonical name has an underscore between
        // "signature" and "recipients" — see SignatureRecipientConfiguration.
        await renamedTableScanner.ScanAsync(
            "documents",
            redactSingleTableAsync: async (renamedTable, innerCt) =>
            {
                if (renamedTable.StartsWith("documents_signature_recipients_del_", StringComparison.Ordinal))
                {
                    return await ExecuteScrubAsync(
                        $"""UPDATE "{renamedTable}" SET "Name" = @placeholder, "Email" = @placeholder, "IpAddress" = NULL WHERE "ContactId" = @contactId""",
                        @event.ContactId, innerCt);
                }
                if (renamedTable.StartsWith("documents_documents_del_", StringComparison.Ordinal))
                {
                    // Mirror the canonical-table behavior: keep the
                    // document, drop the contact back-reference.
                    return await ExecuteUnlinkAsync(
                        $"""UPDATE "{renamedTable}" SET "LinkedEntityId" = NULL, "LinkedEntityType" = NULL WHERE "LinkedEntityType" = 'Contact' AND "LinkedEntityId" = @contactId""",
                        @event.ContactId, innerCt);
                }
                return 0;
            },
            ct: ct);

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Completed ContactGdprDeletedIntegrationEvent for TenantId {TenantId}, ContactId {ContactId}: " +
            "UnlinkedDocuments {UnlinkedCount}, ScrubbedRecipients {ScrubbedCount}",
            @event.TenantId, @event.ContactId, unlinkedCount, scrubbedCount);
    }

    private Task<int> ExecuteScrubAsync(string sql, Guid contactId, CancellationToken ct)
        => RunUpdateAsync(sql, contactId, withPlaceholder: true, ct);

    private Task<int> ExecuteUnlinkAsync(string sql, Guid contactId, CancellationToken ct)
        => RunUpdateAsync(sql, contactId, withPlaceholder: false, ct);

    private async Task<int> RunUpdateAsync(string sql, Guid contactId, bool withPlaceholder, CancellationToken ct)
    {
        var conn = dbContext.Database.GetDbConnection();
        var openedHere = conn.State != System.Data.ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (withPlaceholder)
            {
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@placeholder"; p1.Value = PiiRedactedPlaceholder.Value;
                cmd.Parameters.Add(p1);
            }
            var p2 = cmd.CreateParameter();
            p2.ParameterName = "@contactId"; p2.Value = contactId;
            cmd.Parameters.Add(p2);
            return await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            // EF-owned pooled connection: only close it if we opened it.
            // Disposing would yank it from the pool prematurely.
            if (openedHere) await conn.CloseAsync();
        }
    }
}
