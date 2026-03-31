using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactUpdatedEvent and enqueues integration event to the outbox.</summary>
public sealed class ContactUpdatedDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactUpdatedDomainEventHandler> logger) : INotificationHandler<ContactUpdatedEvent>
{
    /// <summary>
    /// Handles a <see cref="ContactUpdatedEvent"/> by enqueuing a <see cref="ContactUpdatedIntegrationEvent"/> to the transactional outbox.
    /// </summary>
    public async Task Handle(ContactUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling ContactUpdatedEvent for contact {ContactId}",
                notification.ContactId.Value);
            return;
        }

        var integrationEvent = new ContactUpdatedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            ContactId = notification.ContactId.Value
        };

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(ContactUpdatedIntegrationEvent), integrationEvent.TenantId);
    }
}
