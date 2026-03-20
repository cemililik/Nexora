using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Bridges domain events to integration events for cross-module consumption.
/// Publishes NotificationSent, NotificationDelivered, and NotificationBounced events via the event bus.
/// </summary>
public sealed class NotificationSentDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationSentDomainEventHandler> logger) : INotificationHandler<NotificationSentEvent>
{
    /// <summary>
    /// Handles a <see cref="NotificationSentEvent"/> by publishing a <see cref="NotificationSentIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(NotificationSentEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling NotificationSentEvent for notification {NotificationId}",
                notification.NotificationId.Value);
            return;
        }

        var integrationEvent = new NotificationSentIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            NotificationId = notification.NotificationId.Value,
            Channel = notification.Channel.ToString(),
            RecipientCount = notification.RecipientCount
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);

        logger.LogDebug("NotificationSent context — NotificationId: {NotificationId}, Channel: {Channel}, RecipientCount: {RecipientCount}",
            notification.NotificationId.Value, notification.Channel, notification.RecipientCount);
    }
}
