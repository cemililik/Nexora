using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Bridges domain events to integration events for cross-module consumption.
/// Enqueues NotificationSentIntegrationEvent to the outbox for reliable publishing.
/// </summary>
public sealed class NotificationSentDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationSentDomainEventHandler> logger) : INotificationHandler<NotificationSentEvent>
{
    /// <summary>
    /// Handles a <see cref="NotificationSentEvent"/> by enqueuing a <see cref="NotificationSentIntegrationEvent"/> to the transactional outbox.
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

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(NotificationSentIntegrationEvent), integrationEvent.TenantId);
    }
}
