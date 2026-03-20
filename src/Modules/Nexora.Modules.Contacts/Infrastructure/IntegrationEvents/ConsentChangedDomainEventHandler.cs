using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ConsentChangedEvent and publishes integration event.</summary>
public sealed class ConsentChangedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ConsentChangedDomainEventHandler> logger) : INotificationHandler<ConsentChangedEvent>
{
    public async Task Handle(ConsentChangedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new ConsentChangedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = notification.ContactId.Value,
            ConsentType = notification.ConsentType.ToString(),
            Granted = notification.Granted
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ConsentChangedIntegrationEvent for {ContactId}: {ConsentType} = {Granted}",
            notification.ContactId, notification.ConsentType, notification.Granted);
    }
}
