using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>Enqueues NotificationBouncedIntegrationEvent to the outbox when an email bounces.</summary>
public sealed class NotificationBouncedDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationBouncedDomainEventHandler> logger) : INotificationHandler<NotificationBouncedEvent>
{
    /// <summary>
    /// Handles a <see cref="NotificationBouncedEvent"/> by publishing a <see cref="NotificationBouncedIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(NotificationBouncedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling NotificationBouncedEvent for notification {NotificationId}",
                notification.NotificationId.Value);
            return;
        }

        var integrationEvent = new NotificationBouncedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            NotificationId = notification.NotificationId.Value,
            ContactId = notification.ContactId,
            Email = notification.Email
        };

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(NotificationBouncedIntegrationEvent), integrationEvent.TenantId);
    }
}
