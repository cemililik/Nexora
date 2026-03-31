using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ConsentChangedEvent and enqueues integration event to the outbox.</summary>
public sealed class ConsentChangedDomainEventHandler(
    IOutbox outbox,
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

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(ConsentChangedIntegrationEvent), integrationEvent.TenantId);
    }
}
