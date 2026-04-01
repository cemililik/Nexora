using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactMergedEvent and enqueues integration event to the outbox.</summary>
public sealed class ContactMergedDomainEventHandler(
    IOutbox outbox,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactMergedDomainEventHandler> logger) : INotificationHandler<ContactMergedEvent>
{
    /// <summary>
    /// Handles a <see cref="ContactMergedEvent"/> by enqueuing a <see cref="ContactMergedIntegrationEvent"/> to the transactional outbox.
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

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for tenant {TenantId}",
            nameof(ContactMergedIntegrationEvent), integrationEvent.TenantId);
    }
}
