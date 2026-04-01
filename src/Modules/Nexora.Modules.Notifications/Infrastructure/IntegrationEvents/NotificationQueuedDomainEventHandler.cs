using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Bridges the NotificationQueuedEvent domain event to the outbox for reliable Kafka publishing.
/// Enqueues a NotificationDeliveryRequestedIntegrationEvent so the delivery consumer can pick it up.
/// </summary>
public sealed class NotificationQueuedDomainEventHandler(
    IOutbox outbox,
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationQueuedDomainEventHandler> logger) : INotificationHandler<NotificationQueuedEvent>
{
    /// <summary>
    /// Handles a <see cref="NotificationQueuedEvent"/> by publishing a
    /// <see cref="NotificationDeliveryRequestedIntegrationEvent"/> to the outbox.
    /// </summary>
    public async Task Handle(NotificationQueuedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling NotificationQueuedEvent for notification {NotificationId}",
                notification.NotificationId.Value);
            return;
        }

        var entity = await dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notification.NotificationId, cancellationToken);

        if (entity is null)
        {
            logger.LogWarning("Notification {NotificationId} not found when handling NotificationQueuedEvent",
                notification.NotificationId.Value);
            return;
        }

        var isBulk = entity.TriggeredBy == TriggerSource.BulkApi;

        var integrationEvent = new NotificationDeliveryRequestedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            NotificationId = notification.NotificationId.Value,
            Channel = notification.Channel.ToString(),
            IsBulk = isBulk
        };

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for notification {NotificationId} in tenant {TenantId}",
            nameof(NotificationDeliveryRequestedIntegrationEvent), notification.NotificationId.Value, integrationEvent.TenantId);
    }
}
