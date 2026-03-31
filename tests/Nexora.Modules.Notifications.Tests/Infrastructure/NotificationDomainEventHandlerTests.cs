using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class NotificationDomainEventHandlerTests
{
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IOutbox _outbox;
    private readonly Guid _tenantId = Guid.NewGuid();

    public NotificationDomainEventHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, Guid.NewGuid());
        _outbox = Substitute.For<IOutbox>();
    }

    [Fact]
    public async Task NotificationSentHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationSentDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationSentDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var domainEvent = new NotificationSentEvent(notificationId, NotificationChannel.Email, 5);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationSentIntegrationEvent>(e =>
                e.NotificationId == notificationId.Value &&
                e.Channel == "Email" &&
                e.RecipientCount == 5 &&
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDeliveredHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationDeliveredDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationDeliveredDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var recipientId = NotificationRecipientId.New();
        var contactId = Guid.NewGuid();
        var domainEvent = new NotificationDeliveredEvent(notificationId, recipientId, contactId);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationDeliveredIntegrationEvent>(e =>
                e.NotificationId == notificationId.Value &&
                e.RecipientId == recipientId.Value &&
                e.ContactId == contactId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationBouncedHandler_ShouldPublishIntegrationEvent()
    {
        // Arrange
        var handler = new NotificationBouncedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationBouncedDomainEventHandler>.Instance);
        var notificationId = NotificationId.New();
        var contactId = Guid.NewGuid();
        var domainEvent = new NotificationBouncedEvent(notificationId, contactId, "bounced@test.com");

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationBouncedIntegrationEvent>(e =>
                e.NotificationId == notificationId.Value &&
                e.ContactId == contactId &&
                e.Email == "bounced@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationSentHandler_ShouldIncludeTenantId()
    {
        // Arrange
        var handler = new NotificationSentDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationSentDomainEventHandler>.Instance);
        var domainEvent = new NotificationSentEvent(NotificationId.New(), NotificationChannel.Sms, 1);

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationSentIntegrationEvent>(e =>
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationDeliveredHandler_ShouldIncludeTenantId()
    {
        // Arrange
        var handler = new NotificationDeliveredDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationDeliveredDomainEventHandler>.Instance);
        var domainEvent = new NotificationDeliveredEvent(NotificationId.New(), NotificationRecipientId.New(), Guid.NewGuid());

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationDeliveredIntegrationEvent>(e =>
                e.TenantId == _tenantId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotificationBouncedHandler_ShouldIncludeEventId()
    {
        // Arrange
        var handler = new NotificationBouncedDomainEventHandler(
            _outbox, _tenantAccessor, NullLogger<NotificationBouncedDomainEventHandler>.Instance);
        var domainEvent = new NotificationBouncedEvent(NotificationId.New(), Guid.NewGuid(), "test@test.com");

        // Act
        await handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _outbox.Received(1).EnqueueAsync(
            Arg.Is<NotificationBouncedIntegrationEvent>(e =>
                e.EventId != Guid.Empty &&
                e.OccurredAt <= DateTime.UtcNow),
            Arg.Any<CancellationToken>());
    }
}
