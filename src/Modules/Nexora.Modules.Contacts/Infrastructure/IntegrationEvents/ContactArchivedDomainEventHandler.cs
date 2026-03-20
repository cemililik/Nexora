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
    public async Task Handle(ContactArchivedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new ContactArchivedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = notification.ContactId.Value
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ContactArchivedIntegrationEvent for {ContactId}", notification.ContactId);
    }
}
