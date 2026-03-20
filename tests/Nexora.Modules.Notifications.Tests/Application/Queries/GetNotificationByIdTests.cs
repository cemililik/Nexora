using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetNotificationByIdTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetNotificationByIdTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingNotification_ShouldReturnDetail()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipient();
        var handler = new GetNotificationByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationByIdQuery(notification.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Subject.Should().Be("Test Subject");
        result.Value.Recipients.Should().HaveCount(1);
        result.Value.Recipients[0].RecipientAddress.Should().Be("user@test.com");
    }

    [Fact]
    public async Task Handle_NonExistentNotification_ShouldReturnFailure()
    {
        // Arrange
        var handler = new GetNotificationByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_notification_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var notification = Notification.Create(
            Guid.NewGuid(), NotificationChannel.Email, "Subject", "Body", "api");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationByIdQuery(notification.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotificationWithNoRecipients_ShouldReturnEmptyList()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "No Recipients", "Body", "api");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationByIdQuery(notification.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Recipients.Should().BeEmpty();
    }

    private async Task<Notification> SeedNotificationWithRecipient()
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test Subject", "Test Body", "api");
        notification.AddRecipient(Guid.NewGuid(), "user@test.com");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return notification;
    }

    public void Dispose() => _dbContext.Dispose();
}
