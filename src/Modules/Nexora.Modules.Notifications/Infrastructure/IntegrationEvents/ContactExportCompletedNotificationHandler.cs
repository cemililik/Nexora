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
/// event — never produce a second notification.
///
/// <para>
/// <b>T-028 design note.</b> The job previously emitted the notification
/// inline under a <c>startedFromQueued</c> gate; that gate prevented duplicates
/// on resume but produced the dual trade-off of potentially *missing* a
/// notification when a crash landed between <c>MarkProcessing</c> and
/// <c>SendAsync</c>. Moving the send to an inbox-guarded consumer of the
/// existing outbox event closes both gaps at once: the transactional outbox
/// guarantees the event is emitted exactly once per successful export (atomic
/// with the terminal state transition), and the inbox guard on
/// <see cref="IIntegrationEvent.EventId"/> deduplicates any redelivery of that
/// single emission. The logical "<c>contacts:export-ready:{jobId}</c>" dedupe
/// key called out in the T-028 task spec is realised via EventId identity — one
/// EventId escapes the outbox transaction per export job, so EventId dedup is
/// equivalent to a per-job dedup key at the inbox layer.
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
                OrganizationId: null), ct);
        }
        catch (InvalidOperationException ex)
        {
            // Non-fatal — the export itself succeeded and the user can still
            // see it on the status page. Don't mark inbox-processed: next
            // redelivery may succeed.
            logger.LogWarning(
                ex,
                "Failed to send export-ready notification for job {ExportJobId}; will retry on next redelivery of event {EventId}",
                @event.JobId, @event.EventId);
            return;
        }

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Sent export-ready notification for job {JobId} to user {UserId} in tenant {TenantId}",
            @event.JobId, userId, tenantId);
    }
}
