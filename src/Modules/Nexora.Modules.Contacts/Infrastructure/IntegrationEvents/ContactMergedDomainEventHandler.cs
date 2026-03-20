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
    /// <summary>
    /// Handles a <see cref="ContactMergedEvent"/> by publishing a <see cref="ContactMergedIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(ContactMergedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling ContactMergedEvent for primary contact {PrimaryContactId}",
                notification.PrimaryContactId.Value);
            return;
        }

        var integrationEvent = new ContactMergedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            PrimaryContactId = notification.PrimaryContactId.Value,
            SecondaryContactId = notification.SecondaryContactId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
