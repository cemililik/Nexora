using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>Publishes NotificationBouncedIntegrationEvent when an email bounces.</summary>
public sealed class NotificationBouncedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<NotificationBouncedDomainEventHandler> logger) : INotificationHandler<NotificationBouncedEvent>
{
    public async Task Handle(NotificationBouncedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new NotificationBouncedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            NotificationId = notification.NotificationId.Value,
            ContactId = notification.ContactId,
            Email = notification.Email
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
