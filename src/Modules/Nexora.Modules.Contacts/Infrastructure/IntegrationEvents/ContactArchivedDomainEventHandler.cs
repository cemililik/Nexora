using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactArchivedEvent and publishes integration event.</summary>
public sealed class ContactArchivedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactArchivedDomainEventHandler> logger) : INotificationHandler<ContactArchivedEvent>
{
    /// <summary>
    /// Handles a <see cref="ContactArchivedEvent"/> by publishing a <see cref="ContactArchivedIntegrationEvent"/> to the event bus.
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

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
