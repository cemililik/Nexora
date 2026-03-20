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
    public async Task Handle(ContactCreatedEvent notification, CancellationToken cancellationToken)
    {
        var contact = await dbContext.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == notification.ContactId, cancellationToken);

        if (contact is null) return;

        var integrationEvent = new ContactCreatedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = contact.Id.Value,
            ContactType = contact.Type.ToString(),
            Email = contact.Email ?? string.Empty,
            DisplayName = contact.DisplayName
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
