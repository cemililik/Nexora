using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Gdpr;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Constants;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles <see cref="ContactGdprDeletedIntegrationEvent"/> from the Contacts module.
/// Scrubs PII from Notification aggregates that were sent to the erased contact:
/// redacts every <see cref="Domain.Entities.NotificationRecipient.RecipientAddress"/>
/// for the contact and clears the rendered body on each parent <see cref="Domain.Entities.Notification"/>.
/// Preserves <c>template_key</c>, delivery status, timestamps, and counters — these retain
/// audit value and are not personal data per the Notifications PII retention policy.
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandler(
    NotificationsDbContext dbContext,
    IInboxGuard inboxGuard,
    IGdprRenamedTableScanner<NotificationsDbContext> renamedTableScanner,
    ILogger<ContactGdprDeletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>
{
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

        if (!Guid.TryParse(@event.TenantId, out var tenantId))
        {
            logger.LogWarning(
                "GDPR erasure event {EventId} has invalid TenantId {TenantId}; marking processed to prevent redelivery loop",
                @event.EventId, @event.TenantId);
            inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Wrap all redaction work + inbox mark in one transaction on relational
        // providers so partial scrubs cannot commit without the inbox row (and
        // vice-versa). InMemory in tests gets tx = null and SaveChanges is
        // already atomic for EF-tracked domain-method changes.
        await using var tx = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(ct)
            : null;

        int scrubbedRecipientCount;
        int scrubbedNotificationCount;

        if (dbContext.Database.IsRelational())
        {
            // Scope parent-notification updates to the tenant via a subquery over recipients.
            // Execute the notifications update BEFORE the recipient scrub so the subquery
            // still sees the intact recipient rows — ContactId is not touched by the scrub
            // in the current implementation, but ordering is preserved defensively.
            scrubbedNotificationCount = await dbContext.Notifications
                .Where(n => n.TenantId == tenantId
                         && dbContext.NotificationRecipients
                             .Where(r => r.ContactId == @event.ContactId)
                             .Select(r => r.NotificationId)
                             .Contains(n.Id))
                .ExecuteUpdateAsync(setters => setters
                    // T-017: BodyRendered is nullable — write null so auditors can
                    // distinguish an erased row from real content. Subject stays a
                    // placeholder (column remains NOT NULL) to preserve listing UX.
                    .SetProperty(n => n.BodyRendered, (string?)null)
                    .SetProperty(n => n.Subject, PiiRedactedPlaceholder.Value),
                    ct);

            scrubbedRecipientCount = await dbContext.NotificationRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.RecipientAddress, PiiRedactedPlaceholder.Value)
                    .SetProperty(r => r.FailureReason, (string?)null),
                    ct);

            if (scrubbedRecipientCount == 0 && scrubbedNotificationCount == 0)
            {
                inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
                await dbContext.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                logger.LogInformation(
                    "No Notification recipients found for erased contact {ContactId} in tenant {TenantId}; inbox marked",
                    @event.ContactId, tenantId);
                return;
            }
        }
        else
        {
            // InMemory fallback — load + domain methods so tests continue to pass.
            var recipients = await dbContext.NotificationRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ToListAsync(ct);

            if (recipients.Count == 0)
            {
                inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation(
                    "No Notification recipients found for erased contact {ContactId} in tenant {TenantId}; inbox marked",
                    @event.ContactId, tenantId);
                return;
            }

            var notificationIds = recipients.Select(r => r.NotificationId).Distinct().ToList();

            var notifications = await dbContext.Notifications
                .Where(n => notificationIds.Contains(n.Id) && n.TenantId == tenantId)
                .ToListAsync(ct);

            var allowedNotificationIds = notifications.Select(n => n.Id).ToHashSet();

            scrubbedRecipientCount = 0;
            foreach (var recipient in recipients)
            {
                if (!allowedNotificationIds.Contains(recipient.NotificationId))
                {
                    continue;
                }

                recipient.ScrubRecipientAddress();
                scrubbedRecipientCount++;
            }

            foreach (var notification in notifications)
            {
                notification.ScrubRenderedBody();
            }

            scrubbedNotificationCount = notifications.Count;
        }

        // T-027: GDPR escape hatch — redact PII in any notifications_*_del_*
        // tables left behind by an earlier module uninstall (ADR-0028
        // retention window). Two PII-bearing tables exist:
        //   - notifications_recipients_*: RecipientAddress (PII) + FailureReason
        //   - notifications_notifications_*: Subject (PII) + BodyRendered (PII)
        // The notifications_*_del_TS pair shares a timestamp suffix because
        // they were renamed in the same uninstall transaction; the parent
        // table scrub joins the sibling renamed recipients table so it
        // stays scoped to this contact's rows (matches the canonical-table
        // behavior above). Sibling existence is verified via the scanner's
        // discovered-set BEFORE issuing the JOIN — without that, an
        // orphaned renamed parent without its sibling would raise Postgres
        // 42P01 and the scanner's NpgsqlException catch would swallow it
        // along with any real DB errors (review round-2 finding).
        var renamedScan = await renamedTableScanner.ScanAsync(
            "notifications",
            redactSingleTableAsync: async (info, innerCt) =>
            {
                // notifications_recipients_del_<ts> : mask recipient PII.
                if (info.BareName.StartsWith("notifications_recipients_del_", StringComparison.Ordinal))
                {
                    return await ExecuteParameterisedUpdateAsync(
                        $"""UPDATE {info.QualifiedIdentifier} SET "RecipientAddress" = @placeholder, "FailureReason" = NULL WHERE "ContactId" = @contactId""",
                        @event.ContactId, innerCt);
                }
                // notifications_notifications_del_<ts> : scope to the
                // contact's notifications via the sibling renamed
                // recipients table (same timestamp suffix). Skip when
                // the sibling is absent.
                const string parentPrefix = "notifications_notifications_del_";
                if (info.BareName.StartsWith(parentPrefix, StringComparison.Ordinal))
                {
                    var ts = info.BareName[parentPrefix.Length..];
                    var siblingBare = $"notifications_recipients_del_{ts}";
                    if (!info.AllDiscoveredBareNames.Contains(siblingBare))
                    {
                        logger.LogDebug(
                            "GDPR escape hatch: skipping renamed table {Parent} for contact {ContactId} — sibling {Sibling} not in discovered set; cannot reconstruct recipient subquery.",
                            info.BareName, @event.ContactId, siblingBare);
                        return 0;
                    }
                    // Sibling identifier needs to be qualified with the
                    // same schema as the parent — derive it from the
                    // parent's QualifiedIdentifier (`"schema"."parent"`).
                    // Guard: QualifiedIdentifier must contain the quoted BareName so the
                    // Replace produces a valid sibling identifier. The scanner builds
                    // it as `"schema"."bareName"` so this should always hold — but
                    // fail fast on any unexpected shape rather than silently emitting
                    // broken SQL.
                    var quotedBareName = $"\"{info.BareName}\"";
                    if (!info.QualifiedIdentifier.Contains(quotedBareName, StringComparison.Ordinal))
                    {
                        logger.LogWarning(
                            "GDPR escape hatch: QualifiedIdentifier {QualifiedIdentifier} does not contain expected quoted bare name {QuotedBareName}; skipping renamed table {Table}.",
                            info.QualifiedIdentifier, quotedBareName, info.BareName);
                        return 0;
                    }
                    var qualifiedSibling = info.QualifiedIdentifier.Replace(
                        quotedBareName, $"\"{siblingBare}\"", StringComparison.Ordinal);
                    var sql =
                        $"""
                        UPDATE {info.QualifiedIdentifier}
                        SET "BodyRendered" = NULL, "Subject" = @placeholder
                        WHERE "Id" IN (SELECT "NotificationId" FROM {qualifiedSibling} WHERE "ContactId" = @contactId)
                        """;
                    return await ExecuteParameterisedUpdateAsync(sql, @event.ContactId, innerCt);
                }
                return 0;
            },
            ct: ct);

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        // Do NOT log @event.Reason — it is free-text user input and can contain PII
        // (names, emails, phone numbers). EventId + ContactId + counts are sufficient for audit.
        // Renamed-table totals are reported separately so an operator can
        // tell from the log line whether the escape hatch did any work.
        logger.LogInformation(
            "GDPR erasure event {EventId} for contact {ContactId} in tenant {TenantId}: " +
            "scrubbed {RecipientCount} recipients across {NotificationCount} notifications " +
            "(canonical) + {RenamedRowsRedacted} rows across {RenamedTablesScanned} renamed _del_ tables (mode={Mode})",
            @event.EventId, @event.ContactId, tenantId,
            scrubbedRecipientCount, scrubbedNotificationCount,
            renamedScan.RowsRedacted, renamedScan.TablesScanned, @event.Mode);
    }

    private async Task<int> ExecuteParameterisedUpdateAsync(string sql, Guid contactId, CancellationToken ct)
    {
        var conn = dbContext.Database.GetDbConnection();
        var openedHere = conn.State != System.Data.ConnectionState.Open;
        if (openedHere) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            // Enlist in the ambient EF transaction if the caller has one
            // open — without this the raw UPDATE would auto-commit while
            // surrounding EF SaveChanges still rides on the transaction,
            // creating non-atomic GDPR redaction (review round-2).
            var ambientTx = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            if (ambientTx is not null) cmd.Transaction = ambientTx;
            var p1 = cmd.CreateParameter();
            p1.ParameterName = "@placeholder";
            p1.Value = PiiRedactedPlaceholder.Value;
            cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter();
            p2.ParameterName = "@contactId";
            p2.Value = contactId;
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
