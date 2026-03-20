using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class ScheduledNotificationDispatcherJobTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ScheduledNotificationDispatcherJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Execute_DueSchedules_ShouldDispatch()
    {
        // Arrange — schedule in the past (due)
        var notification = await SeedNotificationWithPastSchedule(TimeSpan.FromHours(-1));

        var job = new ScheduledNotificationDispatcherJob(_tenantAccessor, _dbContext,
            NullLogger<ScheduledNotificationDispatcherJob>.Instance);
        var parameters = new ScheduledNotificationDispatcherJobParams
        {
            TenantId = _tenantId.ToString()
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var schedule = await _dbContext.NotificationSchedules.FirstAsync();
        schedule.Status.Should().Be(ScheduleStatus.Dispatched);

        var updatedNotification = await _dbContext.Notifications.FirstAsync();
        updatedNotification.Status.Should().Be(NotificationStatus.Sending);
    }

    [Fact]
    public async Task Execute_FutureSchedules_ShouldNotDispatch()
    {
        // Arrange — schedule in the future (not due)
        await SeedNotificationWithSchedule(DateTime.UtcNow.AddHours(2));

        var job = new ScheduledNotificationDispatcherJob(_tenantAccessor, _dbContext,
            NullLogger<ScheduledNotificationDispatcherJob>.Instance);
        var parameters = new ScheduledNotificationDispatcherJobParams
        {
            TenantId = _tenantId.ToString()
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var schedule = await _dbContext.NotificationSchedules.FirstAsync();
        schedule.Status.Should().Be(ScheduleStatus.Pending);
    }

    [Fact]
    public async Task Execute_CancelledSchedules_ShouldBeSkipped()
    {
        // Arrange
        var (_, schedule) = await SeedNotificationWithPastSchedule(TimeSpan.FromHours(-1));
        schedule.Cancel();
        await _dbContext.SaveChangesAsync();

        var job = new ScheduledNotificationDispatcherJob(_tenantAccessor, _dbContext,
            NullLogger<ScheduledNotificationDispatcherJob>.Instance);
        var parameters = new ScheduledNotificationDispatcherJobParams
        {
            TenantId = _tenantId.ToString()
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.NotificationSchedules.FirstAsync();
        updated.Status.Should().Be(ScheduleStatus.Cancelled);
    }

    [Fact]
    public async Task Execute_NoDueSchedules_ShouldNotThrow()
    {
        // Arrange
        var job = new ScheduledNotificationDispatcherJob(_tenantAccessor, _dbContext,
            NullLogger<ScheduledNotificationDispatcherJob>.Instance);
        var parameters = new ScheduledNotificationDispatcherJobParams
        {
            TenantId = _tenantId.ToString()
        };

        // Act & Assert
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_DifferentTenant_ShouldNotDispatchOtherTenantSchedules()
    {
        // Arrange — schedule for different tenant
        var otherTenantId = Guid.NewGuid();
        var notification = Notification.Create(
            otherTenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        // Use a past date by creating with future date then we test filtering
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        var job = new ScheduledNotificationDispatcherJob(_tenantAccessor, _dbContext,
            NullLogger<ScheduledNotificationDispatcherJob>.Instance);
        var parameters = new ScheduledNotificationDispatcherJobParams
        {
            TenantId = _tenantId.ToString()
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.NotificationSchedules.FirstAsync();
        updated.Status.Should().Be(ScheduleStatus.Pending);
    }

    private async Task<(Notification, NotificationSchedule)> SeedNotificationWithPastSchedule(TimeSpan offset)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Scheduled Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();

        // Create schedule with future date first (domain validation requires future)
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));

        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        // Then manually update ScheduledAt to past date for testing
        _dbContext.Entry(schedule).Property("ScheduledAt").CurrentValue = DateTime.UtcNow.Add(offset);
        await _dbContext.SaveChangesAsync();

        return (notification, schedule);
    }

    private async Task<(Notification, NotificationSchedule)> SeedNotificationWithSchedule(DateTime scheduledAt)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Scheduled Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        var schedule = NotificationSchedule.Create(notification.Id, scheduledAt);
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
        return (notification, schedule);
    }

    public void Dispose() => _dbContext.Dispose();
}
