using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class TestNotificationProviderTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public TestNotificationProviderTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ActiveProvider_ShouldReturnSuccess()
    {
        // Arrange
        var provider = await SeedProvider();
        var handler = new TestNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<TestNotificationProviderHandler>.Instance);
        var command = new TestNotificationProviderCommand(provider.Id.Value, "test@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message!.Key.Should().Be("lockey_notifications_provider_test_initiated");
    }

    [Fact]
    public async Task Handle_NonExistentProvider_ShouldReturnFailure()
    {
        // Arrange
        var handler = new TestNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<TestNotificationProviderHandler>.Instance);
        var command = new TestNotificationProviderCommand(Guid.NewGuid(), "test@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_provider_not_found");
    }

    [Fact]
    public async Task Handle_InactiveProvider_ShouldReturnFailure()
    {
        // Arrange
        var provider = await SeedProvider();
        provider.Deactivate();
        await _dbContext.SaveChangesAsync();

        var handler = new TestNotificationProviderHandler(_dbContext, _tenantAccessor,
            NullLogger<TestNotificationProviderHandler>.Instance);
        var command = new TestNotificationProviderCommand(provider.Id.Value, "test@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_provider_inactive");
    }

    private async Task<NotificationProvider> SeedProvider()
    {
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", 1000);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();
        return provider;
    }

    public void Dispose() => _dbContext.Dispose();
}
