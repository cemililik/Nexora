using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class IdentityEventHandlerTests
{
    private readonly INotificationService _notificationService;
    private readonly UserCreatedIntegrationEventHandler _handler;

    public IdentityEventHandlerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _handler = new UserCreatedIntegrationEventHandler(
            _notificationService, NullLogger<UserCreatedIntegrationEventHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldSendWelcomeNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var @event = new UserCreatedIntegrationEvent
        {
            TenantId = Guid.NewGuid().ToString(),
            UserId = userId,
            Email = "newuser@test.com"
        };

        _notificationService.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r =>
                r.TemplateCode == "welcome" &&
                r.Channel == "Email" &&
                r.ContactId == userId &&
                r.RecipientAddress == "newuser@test.com" &&
                r.Variables["email"] == "newuser@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenSendFails_ShouldNotThrow()
    {
        // Arrange
        var @event = new UserCreatedIntegrationEvent
        {
            TenantId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            Email = "fail@test.com"
        };

        _notificationService.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.Empty);

        // Act & Assert — should not throw
        await _handler.HandleAsync(@event, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseWelcomeTemplateCode()
    {
        // Arrange
        var @event = new UserCreatedIntegrationEvent
        {
            TenantId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            Email = "user@test.com"
        };

        _notificationService.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r => r.TemplateCode == "welcome"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldPassEmailInVariables()
    {
        // Arrange
        var @event = new UserCreatedIntegrationEvent
        {
            TenantId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid(),
            Email = "specific@test.com"
        };

        _notificationService.SendAsync(Arg.Any<SendNotificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _notificationService.Received(1).SendAsync(
            Arg.Is<SendNotificationRequest>(r =>
                r.Variables.ContainsKey("email") &&
                r.Variables["email"] == "specific@test.com"),
            Arg.Any<CancellationToken>());
    }
}
