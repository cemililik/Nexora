using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.Events;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.IntegrationEvents;
using Nexora.Modules.Contacts.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Contacts.Tests.Infrastructure;

public sealed class ContactDomainEventHandlerTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TestEventBus _eventBus;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactDomainEventHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _eventBus = new TestEventBus();
        var options = new DbContextOptionsBuilder<ContactsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ContactsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task ContactCreatedHandler_WhenContactExists_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var contact = Contact.Create(_tenantId, _orgId, ContactType.Individual, "John", "Doe", null, "john@test.com", null, ContactSource.Manual);
        await _dbContext.Contacts.AddAsync(contact);
        await _dbContext.SaveChangesAsync();

        var handler = new ContactCreatedDomainEventHandler(
            _eventBus, _dbContext, _tenantAccessor, NullLogger<ContactCreatedDomainEventHandler>.Instance);

        // Act
        await handler.Handle(new ContactCreatedEvent(contact.Id, ContactType.Individual, "john@test.com"), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        _eventBus.PublishedEvents[0].Should().BeOfType<ContactCreatedIntegrationEvent>();
        var evt = (ContactCreatedIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.ContactId.Should().Be(contact.Id.Value);
        evt.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public async Task ContactUpdatedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactUpdatedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<ContactUpdatedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ContactUpdatedEvent(contactId), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        _eventBus.PublishedEvents[0].Should().BeOfType<ContactUpdatedIntegrationEvent>();
    }

    [Fact]
    public async Task ContactArchivedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactArchivedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<ContactArchivedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ContactArchivedEvent(contactId), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        _eventBus.PublishedEvents[0].Should().BeOfType<ContactArchivedIntegrationEvent>();
    }

    [Fact]
    public async Task ContactMergedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactMergedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<ContactMergedDomainEventHandler>.Instance);

        // Act
        var primaryId = ContactId.New();
        var secondaryId = ContactId.New();
        await handler.Handle(new ContactMergedEvent(primaryId, secondaryId), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        var evt = (ContactMergedIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.PrimaryContactId.Should().Be(primaryId.Value);
        evt.SecondaryContactId.Should().Be(secondaryId.Value);
    }

    [Fact]
    public async Task ConsentChangedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ConsentChangedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<ConsentChangedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ConsentChangedEvent(contactId, ConsentType.EmailMarketing, true), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        var evt = (ConsentChangedIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.ContactId.Should().Be(contactId.Value);
        evt.ConsentType.Should().Be("EmailMarketing");
        evt.Granted.Should().BeTrue();
    }

    [Fact]
    public async Task ContactCreatedHandler_ContactNotFound_ShouldNotPublish()
    {
        // Arrange
        var handler = new ContactCreatedDomainEventHandler(
            _eventBus, _dbContext, _tenantAccessor, NullLogger<ContactCreatedDomainEventHandler>.Instance);

        // Act
        await handler.Handle(new ContactCreatedEvent(ContactId.New(), ContactType.Individual, "test@test.com"), CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();

    private sealed class TestEventBus : IEventBus
    {
        public List<IIntegrationEvent> PublishedEvents { get; } = [];

        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
            where TEvent : IIntegrationEvent
        {
            PublishedEvents.Add(@event);
            return Task.CompletedTask;
        }
    }
}
