using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactMergedEvent and publishes integration event.</summary>
public sealed class ContactMergedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactMergedDomainEventHandler> logger) : INotificationHandler<ContactMergedEvent>
{
    public async Task Handle(ContactMergedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new ContactMergedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            PrimaryContactId = notification.PrimaryContactId.Value,
            SecondaryContactId = notification.SecondaryContactId.Value
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ContactMergedIntegrationEvent: {SecondaryId} merged into {PrimaryId}",
            notification.SecondaryContactId, notification.PrimaryContactId);
    }
}
