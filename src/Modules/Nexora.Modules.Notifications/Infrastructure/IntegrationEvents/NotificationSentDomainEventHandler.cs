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
    public async Task Handle(NotificationSentEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new NotificationSentIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            NotificationId = notification.NotificationId.Value,
            Channel = notification.Channel.ToString(),
            RecipientCount = notification.RecipientCount
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Published NotificationSentIntegrationEvent for notification {NotificationId} with {RecipientCount} recipients",
            notification.NotificationId, notification.RecipientCount);
    }
}
