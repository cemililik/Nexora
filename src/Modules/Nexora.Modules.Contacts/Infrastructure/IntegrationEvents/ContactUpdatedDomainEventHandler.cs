using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactUpdatedEvent and publishes integration event.</summary>
public sealed class ContactUpdatedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactUpdatedDomainEventHandler> logger) : INotificationHandler<ContactUpdatedEvent>
{
    public async Task Handle(ContactUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new ContactUpdatedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = notification.ContactId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
