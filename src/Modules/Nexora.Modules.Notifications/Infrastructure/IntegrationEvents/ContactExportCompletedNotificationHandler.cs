using System.Globalization;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles <see cref="ContactExportCompletedIntegrationEvent"/> from the Contacts
/// module. Delivers the export-ready in-app notification to the user that
/// triggered the export. The handler is inbox-guarded so Hangfire retries of the
/// parent <c>ContactExportJob</c> — or Dapr pub/sub redeliveries of the same
/// event — are effectively deduplicated in the common case.
///
/// <para>
/// <b>Delivery semantics — at-least-once with inbox dedup (NOT exactly-once).</b>
/// The transactional outbox guarantees at least one emission per successful
/// export (atomic with the terminal state transition). The inbox guard on
/// <see cref="IIntegrationEvent.EventId"/> deduplicates redeliveries of that
/// emission. However, <c>INotificationService.SendAsync</c> and the subsequent
/// <c>dbContext.SaveChangesAsync</c> (which commits the inbox MarkAsProcessed
/// row) are NOT atomic — a crash or Dapr channel drop in the window after
/// <c>SendAsync</c> succeeds but before the inbox row commits will, on
/// redelivery, result in a DUPLICATE notification. This residual window is
/// the practical limit of outbox + inbox; the user-visible consequence is
/// one duplicate export-ready notification in rare crash scenarios, which
/// is strictly preferable to the previous design's potential miss.
/// </para>
///
/// <para>
/// <b>T-028 design note.</b> The job previously emitted the notification
/// inline under a <c>startedFromQueued</c> gate; that gate prevented duplicates
/// on resume but produced the dual trade-off of potentially *missing* a
/// notification when a crash landed between <c>MarkProcessing</c> and
/// <c>SendAsync</c>. Moving the send to an inbox-guarded consumer of the
/// existing outbox event trades that miss risk for a much smaller duplicate
/// risk (the at-most-once window above) — the explicit preference per
/// T-028's design. The logical "<c>contacts:export-ready:{jobId}</c>" dedupe
/// key called out in the T-028 task spec is realised via EventId identity —
/// one EventId escapes the outbox transaction per export job, so EventId
/// dedup is equivalent to a per-job dedup key at the inbox layer.
/// </para>
/// </summary>
public sealed class ContactExportCompletedNotificationHandler(
    NotificationsDbContext dbContext,
    INotificationService notificationService,
    IInboxGuard inboxGuard,
    ILogger<ContactExportCompletedNotificationHandler> logger)
    : IIntegrationEventHandler<ContactExportCompletedIntegrationEvent>
{
    /// <inheritdoc />
    public async Task HandleAsync(ContactExportCompletedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug(
                "Skipping duplicate event {EventId} of type {EventType}",
                @event.EventId, @event.GetType().Name);
            return;
        }

        // No TriggeredByUserId means an anonymous or system-initiated export
        // (e.g. scheduled export job). Nothing to send — the user can still
        // observe completion via the status-polling endpoint. Mark the inbox
        // so redeliveries don't re-check.
        if (@event.TriggeredByUserId is not { } userId)
        {
            inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
            await dbContext.SaveChangesAsync(ct);
            logger.LogDebug(
                "Export {JobId} has no TriggeredByUserId; skipping in-app notification and marking inbox processed",
                @event.JobId);
            return;
        }

        if (!Guid.TryParse(@event.TenantId, out var tenantId))
        {
            logger.LogWarning(
                "Export-ready event {EventId} has invalid TenantId {TenantId}; marking processed to prevent redelivery loop",
                @event.EventId, @event.TenantId);
            inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Send via the existing notification service so template resolution,
        // locale selection, and channel routing stay consistent with other
        // in-app notifications. The notification service itself is idempotent
        // at the persistence layer — but the inbox guard is the authoritative
        // dedupe point; we mark processed only after SendAsync succeeds so a
        // send failure re-runs on redelivery.
        try
        {
            await notificationService.SendAsync(new SendNotificationRequest(
                TemplateCode: "lockey_contacts_notification_export_ready",
                Channel: "in_app",
                ContactId: userId,
                RecipientAddress: userId.ToString(),
                Variables: new Dictionary<string, string>
                {
                    ["jobId"] = @event.JobId.ToString(),
                    ["format"] = @event.Format,
                    ["totalRows"] = @event.TotalRows.ToString(CultureInfo.InvariantCulture)
                },
                OrganizationId: @event.OrganizationId?.ToString()), ct);
        }
        catch (InvalidOperationException ex)
        {
            // Log + rethrow: returning here would cause Dapr to ACK the
            // message and silently drop the redelivery. The inbox row stays
            // unmarked (we didn't reach MarkAsProcessed), and rethrowing
            // signals Dapr to NACK → the broker redelivers the same EventId,
            // the guard short-circuits at the top if the transient error
            // resolved by then (e.g. a template just landed) — giving us
            // real retry behaviour instead of at-most-once silent loss.
            logger.LogWarning(
                ex,
                "Failed to send export-ready notification for job {ExportJobId}; NACKing to trigger redelivery of event {EventId}",
                @event.JobId, @event.EventId);
            throw;
        }

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Sent export-ready notification for job {JobId} to user {UserId} in tenant {TenantId}",
            @event.JobId, userId, tenantId);
    }
}
