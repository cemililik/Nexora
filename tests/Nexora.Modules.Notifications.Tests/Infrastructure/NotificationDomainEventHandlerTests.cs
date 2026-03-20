using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class NotificationDomainEventHandlerTests
{
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TestEventBus _eventBus;
    private readonly Guid _tenantId = Guid.NewGuid();

    public NotificationDomainEventHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, Guid.NewGuid());
        _eventBus = new TestEventBus();
    }

    [Fact]
    public async Task NotificationSentHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationSentDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationSentDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var domainEvent = new NotificationSentEvent(notificationId, NotificationChannel.Email, 5);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        var evt = _eventBus.PublishedEvents[0].Should().BeOfType<NotificationSentIntegrationEvent>().Subject;
        evt.NotificationId.Should().Be(notificationId.Value);
        evt.Channel.Should().Be("Email");
        evt.RecipientCount.Should().Be(5);
        evt.TenantId.Should().Be(_tenantId.ToString());
    }

    [Fact]
    public async Task NotificationDeliveredHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationDeliveredDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationDeliveredDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var recipientId = NotificationRecipientId.New();
        var contactId = Guid.NewGuid();
        var domainEvent = new NotificationDeliveredEvent(notificationId, recipientId, contactId);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        var evt = _eventBus.PublishedEvents[0].Should().BeOfType<NotificationDeliveredIntegrationEvent>().Subject;
        evt.NotificationId.Should().Be(notificationId.Value);
        evt.RecipientId.Should().Be(recipientId.Value);
        evt.ContactId.Should().Be(contactId);
    }

    [Fact]
    public async Task NotificationBouncedHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationBouncedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationBouncedDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var contactId = Guid.NewGuid();
        var domainEvent = new NotificationBouncedEvent(notificationId, contactId, "bounced@test.com");

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        _eventBus.PublishedEvents.Should().HaveCount(1);
        var evt = _eventBus.PublishedEvents[0].Should().BeOfType<NotificationBouncedIntegrationEvent>().Subject;
        evt.NotificationId.Should().Be(notificationId.Value);
        evt.ContactId.Should().Be(contactId);
        evt.Email.Should().Be("bounced@test.com");
    }

    [Fact]
    public async Task NotificationSentHandler_ShouldIncludeTenantId()
    {
        // Arrange
        var handler = new NotificationSentDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationSentDomainEventHandler>.Instance);
        var domainEvent = new NotificationSentEvent(NotificationId.New(), NotificationChannel.Sms, 1);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        var evt = (NotificationSentIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.TenantId.Should().Be(_tenantId.ToString());
    }

    [Fact]
    public async Task NotificationDeliveredHandler_ShouldIncludeTenantId()
    {
        // Arrange
        var handler = new NotificationDeliveredDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationDeliveredDomainEventHandler>.Instance);
        var domainEvent = new NotificationDeliveredEvent(NotificationId.New(), NotificationRecipientId.New(), Guid.NewGuid());

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        var evt = (NotificationDeliveredIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.TenantId.Should().Be(_tenantId.ToString());
    }

    [Fact]
    public async Task NotificationBouncedHandler_ShouldIncludeEventId()
    {
        // Arrange
        var handler = new NotificationBouncedDomainEventHandler(
            _eventBus, _tenantAccessor, NullLogger<NotificationBouncedDomainEventHandler>.Instance);
        var domainEvent = new NotificationBouncedEvent(NotificationId.New(), Guid.NewGuid(), "test@test.com");

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        var evt = (NotificationBouncedIntegrationEvent)_eventBus.PublishedEvents[0];
        evt.EventId.Should().NotBe(Guid.Empty);
        evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

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
