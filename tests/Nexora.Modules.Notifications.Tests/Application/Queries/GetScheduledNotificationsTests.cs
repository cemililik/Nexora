using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetScheduledNotificationsTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetScheduledNotificationsTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithPendingSchedules_ShouldReturnPagedResult()
    {
        // Arrange
        await SeedSchedule(DateTime.UtcNow.AddDays(1));
        await SeedSchedule(DateTime.UtcNow.AddDays(2));
        var handler = new GetScheduledNotificationsHandler(_dbContext, _tenantAccessor, NullLogger<GetScheduledNotificationsHandler>.Instance);
        var query = new GetScheduledNotificationsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnPendingSchedules()
    {
        // Arrange
        var (_, schedule1) = await SeedSchedule(DateTime.UtcNow.AddDays(1));
        var (_, schedule2) = await SeedSchedule(DateTime.UtcNow.AddDays(2));
        schedule2.Cancel();
        await _dbContext.SaveChangesAsync();

        var handler = new GetScheduledNotificationsHandler(_dbContext, _tenantAccessor, NullLogger<GetScheduledNotificationsHandler>.Instance);
        var query = new GetScheduledNotificationsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldNotReturnOtherTenantSchedules()
    {
        // Arrange — seed for different tenant
        var otherTenantId = Guid.NewGuid();
        var notification = Notification.Create(
            otherTenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        var handler = new GetScheduledNotificationsHandler(_dbContext, _tenantAccessor, NullLogger<GetScheduledNotificationsHandler>.Instance);
        var query = new GetScheduledNotificationsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldOrderByScheduledAt()
    {
        // Arrange
        await SeedSchedule(DateTime.UtcNow.AddDays(3));
        await SeedSchedule(DateTime.UtcNow.AddDays(1));
        await SeedSchedule(DateTime.UtcNow.AddDays(2));
        var handler = new GetScheduledNotificationsHandler(_dbContext, _tenantAccessor, NullLogger<GetScheduledNotificationsHandler>.Instance);
        var query = new GetScheduledNotificationsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value!.Items.ToList();
        items[0].ScheduledAt.Should().BeBefore(items[1].ScheduledAt);
        items[1].ScheduledAt.Should().BeBefore(items[2].ScheduledAt);
    }

    private async Task<(Notification, NotificationSchedule)> SeedSchedule(DateTime scheduledAt)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
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
