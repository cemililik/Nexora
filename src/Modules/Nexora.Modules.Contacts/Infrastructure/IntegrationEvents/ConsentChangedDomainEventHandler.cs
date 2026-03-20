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
    /// <summary>
    /// Handles a <see cref="ConsentChangedEvent"/> by publishing a <see cref="ConsentChangedIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(ConsentChangedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling ConsentChangedEvent for contact {ContactId}",
                notification.ContactId.Value);
            return;
        }

        var integrationEvent = new ConsentChangedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            ContactId = notification.ContactId.Value,
            ConsentType = notification.ConsentType.ToString(),
            Granted = notification.Granted
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
