using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class UpdateDeliveryStatusTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateDeliveryStatusTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_Delivered_ShouldUpdateRecipientStatus()
    {
        // Arrange
        var (notification, messageId) = await SeedNotificationWithSentRecipient();
        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);
        var command = new UpdateDeliveryStatusCommand(messageId, "delivered");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var recipient = (await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync())
            .Recipients.First();
        recipient.Status.Should().Be(RecipientStatus.Delivered);
        recipient.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Opened_ShouldUpdateRecipientStatus()
    {
        // Arrange
        var (notification, messageId) = await SeedNotificationWithSentRecipient();
        // First mark delivered, then opened
        var recipient = notification.Recipients.First();
        recipient.MarkDelivered();
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);
        var command = new UpdateDeliveryStatusCommand(messageId, "opened");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Bounced_ShouldUpdateWithReason()
    {
        // Arrange
        var (notification, messageId) = await SeedNotificationWithSentRecipient();
        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);
        var command = new UpdateDeliveryStatusCommand(messageId, "bounced", "Mailbox full");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = (await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync())
            .Recipients.First();
        updated.Status.Should().Be(RecipientStatus.Bounced);
        updated.FailureReason.Should().Be("Mailbox full");
    }

    [Fact]
    public async Task Handle_NonExistentNotification_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);
        var command = new UpdateDeliveryStatusCommand("msg_nonexistent", "delivered");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_notification_not_found");
    }

    [Fact]
    public async Task Handle_NonExistentProviderMessageId_ShouldReturnFailure()
    {
        // Arrange
        await SeedNotificationWithSentRecipient();
        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);
        var command = new UpdateDeliveryStatusCommand("nonexistent_msg", "delivered");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_notification_not_found");
    }

    [Fact]
    public async Task Handle_ShouldUpdateNotificationCounts()
    {
        // Arrange
        var (notification, messageId) = await SeedNotificationWithSentRecipient();
        var handler = new UpdateDeliveryStatusHandler(_dbContext, NullLogger<UpdateDeliveryStatusHandler>.Instance);

        // Act
        await handler.Handle(
            new UpdateDeliveryStatusCommand(messageId, "delivered"),
            CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.FirstAsync();
        updated.DeliveredCount.Should().Be(1);
    }

    private async Task<(Notification notification, string messageId)> SeedNotificationWithSentRecipient()
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test", "Body", "api");
        var recipient = notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();

        var messageId = $"msg_{Guid.NewGuid():N}";
        recipient.MarkSent(messageId);

        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return (notification, messageId);
    }

    public void Dispose() => _dbContext.Dispose();
}
