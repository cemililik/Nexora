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
    /// <summary>
    /// Handles a <see cref="NotificationDeliveredEvent"/> by publishing a <see cref="NotificationDeliveredIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(NotificationDeliveredEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling NotificationDeliveredEvent for notification {NotificationId}",
                notification.NotificationId.Value);
            return;
        }

        var integrationEvent = new NotificationDeliveredIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            NotificationId = notification.NotificationId.Value,
            RecipientId = notification.RecipientId.Value,
            ContactId = notification.ContactId
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
