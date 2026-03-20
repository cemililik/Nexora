using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class CancelScheduledNotificationTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CancelScheduledNotificationTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_PendingSchedule_ShouldCancel()
    {
        // Arrange
        var (_, schedule) = await SeedNotificationWithSchedule();
        var handler = new CancelScheduledNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<CancelScheduledNotificationHandler>.Instance);
        var command = new CancelScheduledNotificationCommand(schedule.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.NotificationSchedules.FirstAsync();
        updated.Status.Should().Be(ScheduleStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_NonExistentSchedule_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CancelScheduledNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<CancelScheduledNotificationHandler>.Instance);
        var command = new CancelScheduledNotificationCommand(Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_schedule_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange — create schedule for different tenant
        var otherTenantId = Guid.NewGuid();
        var notification = Notification.Create(
            otherTenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        var handler = new CancelScheduledNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<CancelScheduledNotificationHandler>.Instance);
        var command = new CancelScheduledNotificationCommand(schedule.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_schedule_not_found");
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessMessage()
    {
        // Arrange
        var (_, schedule) = await SeedNotificationWithSchedule();
        var handler = new CancelScheduledNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<CancelScheduledNotificationHandler>.Instance);
        var command = new CancelScheduledNotificationCommand(schedule.Id.Value);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_notifications_schedule_cancelled");
    }

    private async Task<(Notification, NotificationSchedule)> SeedNotificationWithSchedule()
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
        return (notification, schedule);
    }

    public void Dispose() => _dbContext.Dispose();
}
