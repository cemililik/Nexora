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
using NSubstitute;

namespace Nexora.Modules.Contacts.Tests.Infrastructure;

public sealed class ContactDomainEventHandlerTests : IDisposable
{
    private readonly ContactsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactDomainEventHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        _outbox = Substitute.For<IOutbox>();
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
            _outbox, _dbContext, _tenantAccessor, NullLogger<ContactCreatedDomainEventHandler>.Instance);

        // Act
        await handler.Handle(new ContactCreatedEvent(contact.Id, ContactType.Individual, "john@test.com"), CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactCreatedIntegrationEvent>(e =>
                e.ContactId == contact.Id.Value &&
                e.DisplayName == "John Doe"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContactUpdatedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactUpdatedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<ContactUpdatedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ContactUpdatedEvent(contactId), CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Any<ContactUpdatedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContactArchivedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactArchivedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<ContactArchivedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ContactArchivedEvent(contactId), CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Any<ContactArchivedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContactMergedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ContactMergedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<ContactMergedDomainEventHandler>.Instance);

        // Act
        var primaryId = ContactId.New();
        var secondaryId = ContactId.New();
        await handler.Handle(new ContactMergedEvent(primaryId, secondaryId), CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ContactMergedIntegrationEvent>(e =>
                e.PrimaryContactId == primaryId.Value &&
                e.SecondaryContactId == secondaryId.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsentChangedHandler_WhenCalled_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new ConsentChangedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<ConsentChangedDomainEventHandler>.Instance);

        // Act
        var contactId = ContactId.New();
        await handler.Handle(new ConsentChangedEvent(contactId, ConsentType.EmailMarketing, true), CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<ConsentChangedIntegrationEvent>(e =>
                e.ContactId == contactId.Value &&
                e.ConsentType == "EmailMarketing" &&
                e.Granted == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ContactCreatedHandler_ContactNotFound_ShouldNotPublish()
    {
        // Arrange
        var handler = new ContactCreatedDomainEventHandler(
            _outbox, _dbContext, _tenantAccessor, NullLogger<ContactCreatedDomainEventHandler>.Instance);

        // Act
        await handler.Handle(new ContactCreatedEvent(ContactId.New(), ContactType.Individual, "test@test.com"), CancellationToken.None);

        // Assert
        await _outbox.DidNotReceive().EnqueueAsync(
            Arg.Any<ContactCreatedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    public void Dispose() => _dbContext.Dispose();
}
