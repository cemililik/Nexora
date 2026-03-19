using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

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

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ContactCreatedIntegrationEvent for {ContactId}", contact.Id);
    }
}

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

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ContactUpdatedIntegrationEvent for {ContactId}", notification.ContactId);
    }
}

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

/// <summary>Handles ConsentChangedEvent and publishes integration event.</summary>
public sealed class ConsentChangedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ConsentChangedDomainEventHandler> logger) : INotificationHandler<ConsentChangedEvent>
{
    public async Task Handle(ConsentChangedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new ConsentChangedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            ContactId = notification.ContactId.Value,
            ConsentType = notification.ConsentType.ToString(),
            Granted = notification.Granted
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published ConsentChangedIntegrationEvent for {ContactId}: {ConsentType} = {Granted}",
            notification.ContactId, notification.ConsentType, notification.Granted);
    }
}
