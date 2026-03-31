using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactArchivedEvent and enqueues integration event to the outbox.</summary>
public sealed class ContactArchivedDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactArchivedDomainEventHandler> logger) : INotificationHandler<ContactArchivedEvent>
{
    /// <summary>
    /// Handles a <see cref="ContactArchivedEvent"/> by enqueuing a <see cref="ContactArchivedIntegrationEvent"/> to the transactional outbox.
    /// </summary>
    public async Task Handle(ContactArchivedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling ContactArchivedEvent for contact {ContactId}",
                notification.ContactId.Value);
            return;
        }

        var integrationEvent = new ContactArchivedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            ContactId = notification.ContactId.Value
        };

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(ContactArchivedIntegrationEvent), integrationEvent.TenantId);
    }
}
