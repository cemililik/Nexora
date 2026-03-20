using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class UpdateNotificationProviderTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateNotificationProviderTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ShouldUpdateProvider()
    {
        // Arrange
        var provider = await SeedProvider();
        var handler = new UpdateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationProviderHandler>.Instance);
        var command = new UpdateNotificationProviderCommand(provider.Id.Value, "{\"newKey\":\"value\"}", 2000, true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DailyLimit.Should().Be(2000);
        result.Value.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentProvider_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationProviderHandler>.Instance);
        var command = new UpdateNotificationProviderCommand(Guid.NewGuid(), "{}", 1000, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_provider_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var otherProvider = NotificationProvider.Create(
            Guid.NewGuid(), NotificationChannel.Email, ProviderName.SendGrid, "{}", 500);
        await _dbContext.NotificationProviders.AddAsync(otherProvider);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationProviderHandler>.Instance);
        var command = new UpdateNotificationProviderCommand(otherProvider.Id.Value, "{}", 1000, false);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistChanges()
    {
        // Arrange
        var provider = await SeedProvider();
        var handler = new UpdateNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationProviderHandler>.Instance);

        // Act
        await handler.Handle(
            new UpdateNotificationProviderCommand(provider.Id.Value, "{\"updated\":true}", 3000, true),
            CancellationToken.None);

        // Assert
        var updated = await _dbContext.NotificationProviders.FirstAsync();
        updated.Config.Should().Be("{\"updated\":true}");
        updated.DailyLimit.Should().Be(3000);
    }

    private async Task<NotificationProvider> SeedProvider()
    {
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{\"apiKey\":\"test\"}", 1000);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();
        return provider;
    }

    public void Dispose() => _dbContext.Dispose();
}
