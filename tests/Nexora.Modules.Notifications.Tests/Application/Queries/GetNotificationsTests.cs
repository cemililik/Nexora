using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetNotificationsTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetNotificationsTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        var handler = new GetNotificationsHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNotifications_ShouldReturnPagedResult()
    {
        // Arrange
        await SeedNotifications(3);
        var handler = new GetNotificationsHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_FilterByChannel_ShouldFilterCorrectly()
    {
        // Arrange
        await SeedNotification(NotificationChannel.Email);
        await SeedNotification(NotificationChannel.Sms);
        var handler = new GetNotificationsHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationsQuery(Channel: "Email"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Channel.Should().Be("Email");
    }

    [Fact]
    public async Task Handle_FilterByStatus_ShouldFilterCorrectly()
    {
        // Arrange
        var queuedNotification = await SeedNotification(NotificationChannel.Email);
        var sendingNotification = await SeedNotification(NotificationChannel.Email);
        sendingNotification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationsHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationsQuery(Status: "Queued"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Handle_ShouldOnlyReturnCurrentTenant()
    {
        // Arrange
        await SeedNotification(NotificationChannel.Email);
        var otherNotification = Notification.Create(
            Guid.NewGuid(), NotificationChannel.Email, "Subject", "Body", "api");
        otherNotification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(otherNotification);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationsHandler(_dbContext, _tenantAccessor);

        // Act
        var result = await handler.Handle(new GetNotificationsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
    }

    private async Task SeedNotifications(int count)
    {
        for (var i = 0; i < count; i++)
            await SeedNotification(NotificationChannel.Email);
    }

    private async Task<Notification> SeedNotification(NotificationChannel channel)
    {
        var notification = Notification.Create(
            _tenantId, channel, $"Subject {Guid.NewGuid():N}", "Body", "api");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return notification;
    }

    public void Dispose() => _dbContext.Dispose();
}
