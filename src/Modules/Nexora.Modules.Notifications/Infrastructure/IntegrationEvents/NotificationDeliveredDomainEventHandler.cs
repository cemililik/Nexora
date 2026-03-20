using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>Publishes NotificationDeliveredIntegrationEvent when a recipient delivery is confirmed.</summary>
public sealed class NotificationDeliveredDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationDeliveredDomainEventHandler> logger) : INotificationHandler<NotificationDeliveredEvent>
{
    public async Task Handle(NotificationDeliveredEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new NotificationDeliveredIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            NotificationId = notification.NotificationId.Value,
            RecipientId = notification.RecipientId.Value,
            ContactId = notification.ContactId
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Published NotificationDeliveredIntegrationEvent for recipient {RecipientId} of notification {NotificationId}",
            notification.RecipientId, notification.NotificationId);
    }
}
