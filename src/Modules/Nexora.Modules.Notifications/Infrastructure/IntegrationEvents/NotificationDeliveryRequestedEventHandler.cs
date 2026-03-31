using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Kafka consumer that handles notification delivery requests.
/// Replaces direct Hangfire job enqueuing with event-driven delivery via the outbox/inbox pattern.
/// Reuses <see cref="DeliveryJobHelper"/> for all delivery logic to avoid duplication.
/// </summary>
public sealed class NotificationDeliveryRequestedEventHandler(
    NotificationsDbContext dbContext,
    IInboxGuard inboxGuard,
    ILogger<NotificationDeliveryRequestedEventHandler> logger) : IIntegrationEventHandler<NotificationDeliveryRequestedIntegrationEvent>
{
    private const int BulkBatchSize = 100;
    private const string SmsMessageIdPrefix = "sms_";
    private const string EmailMessageIdPrefix = "msg_";
    private const string BulkMessageIdPrefix = "bulk_";

    /// <summary>Handles delivery of a single or bulk notification via the configured channel provider.</summary>
    public async Task HandleAsync(NotificationDeliveryRequestedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug("Skipping duplicate event {EventId} of type {EventType}",
                @event.EventId, nameof(NotificationDeliveryRequestedIntegrationEvent));
            return;
        }

        var tenantId = Guid.Parse(@event.TenantId);

        var notification = await DeliveryJobHelper.LoadNotificationAsync(
            dbContext, @event.NotificationId, tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for delivery in tenant {TenantId}",
                @event.NotificationId, @event.TenantId);
            inboxGuard.MarkAsProcessed(@event.EventId, nameof(NotificationDeliveryRequestedIntegrationEvent));
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var channel = Enum.Parse<NotificationChannel>(@event.Channel, ignoreCase: true);

        var provider = await DeliveryJobHelper.FindDefaultProviderAsync(
            dbContext, tenantId, channel, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default provider found for channel {Channel} in tenant {TenantId}",
                @event.Channel, @event.TenantId);
            await DeliveryJobHelper.FailAllPendingAsync(
                dbContext, notification, "lockey_notifications_error_no_active_provider", ct);
            inboxGuard.MarkAsProcessed(@event.EventId, nameof(NotificationDeliveryRequestedIntegrationEvent));
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

        if (@event.IsBulk)
        {
            DeliverBulk(pendingRecipients, notification, provider, ct);
        }
        else
        {
            DeliverSingle(pendingRecipients, notification, provider);
        }

        DeliveryJobHelper.FinalizeNotificationStatus(notification,
            pendingRecipients.Any(r => r.Status == RecipientStatus.Sent),
            pendingRecipients.All(r => r.Status == RecipientStatus.Sent));
        DeliveryJobHelper.UpdateNotificationCounts(notification);

        inboxGuard.MarkAsProcessed(@event.EventId, nameof(NotificationDeliveryRequestedIntegrationEvent));
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Delivery completed for notification {NotificationId} via {Channel} (bulk={IsBulk}) in tenant {TenantId}",
            @event.NotificationId, @event.Channel, @event.IsBulk, @event.TenantId);
    }

    private void DeliverSingle(
        List<NotificationRecipient> pendingRecipients,
        Notification notification,
        NotificationProvider provider)
    {
        var messagePrefix = notification.Channel == NotificationChannel.Sms ? SmsMessageIdPrefix : EmailMessageIdPrefix;
        DeliveryJobHelper.ProcessRecipients(pendingRecipients, provider, messagePrefix, logger);
    }

    private void DeliverBulk(
        List<NotificationRecipient> pendingRecipients,
        Notification notification,
        NotificationProvider provider,
        CancellationToken ct)
    {
        var processedCount = 0;

        foreach (var batch in pendingRecipients.Chunk(BulkBatchSize))
        {
            DeliveryJobHelper.ProcessRecipients(batch, provider, BulkMessageIdPrefix, logger);
            processedCount += batch.Count(r => r.Status == RecipientStatus.Sent);
        }

        logger.LogInformation("Bulk notification {NotificationId} processed: {ProcessedCount}/{TotalCount} recipients",
            notification.Id, processedCount, pendingRecipients.Count);
    }
}
