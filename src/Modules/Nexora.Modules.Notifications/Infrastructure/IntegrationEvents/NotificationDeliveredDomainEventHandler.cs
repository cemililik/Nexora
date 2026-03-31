using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>Enqueues NotificationDeliveredIntegrationEvent to the outbox when a recipient delivery is confirmed.</summary>
public sealed class NotificationDeliveredDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationDeliveredDomainEventHandler> logger) : INotificationHandler<NotificationDeliveredEvent>
{
    /// <summary>
    /// Handles a <see cref="NotificationDeliveredEvent"/> by enqueuing a <see cref="NotificationDeliveredIntegrationEvent"/> to the transactional outbox.
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

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(NotificationDeliveredIntegrationEvent), integrationEvent.TenantId);
    }
}
