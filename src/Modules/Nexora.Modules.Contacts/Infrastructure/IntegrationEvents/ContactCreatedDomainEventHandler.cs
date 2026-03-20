using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;

/// <summary>Handles ContactCreatedEvent and publishes integration event.</summary>
public sealed class ContactCreatedDomainEventHandler(
    IEventBus eventBus,
    ContactsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ContactCreatedDomainEventHandler> logger) : INotificationHandler<ContactCreatedEvent>
{
    /// <summary>
    /// Handles a <see cref="ContactCreatedEvent"/> by publishing a <see cref="ContactCreatedIntegrationEvent"/> to the event bus.
    /// Logs a warning and skips if the contact is not found in the database.
    /// </summary>
    public async Task Handle(ContactCreatedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling ContactCreatedEvent for contact {ContactId}",
                notification.ContactId.Value);
            return;
        }

        var contact = await dbContext.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == notification.ContactId, cancellationToken);

        if (contact is null)
        {
            logger.LogWarning("Contact {ContactId} not found when handling ContactCreatedEvent — skipping integration event",
                notification.ContactId.Value);
            return;
        }

        var integrationEvent = new ContactCreatedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            ContactId = contact.Id.Value,
            ContactType = contact.Type.ToString(),
            Email = contact.Email,
            DisplayName = contact.DisplayName
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
