using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class NotificationCleanupJobTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IActiveTenantProvider _tenantProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public NotificationCleanupJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);

        // Set up PlatformJob infrastructure mocks
        _tenantProvider = Substitute.For<IActiveTenantProvider>();
        _tenantProvider.GetActiveTenantsWithModuleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<ActiveTenantInfo> { new(_tenantId.ToString(), "tenant_test") });

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITenantContextAccessor)).Returns(_tenantAccessor);
        serviceProvider.GetService(typeof(NotificationsDbContext)).Returns(_dbContext);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    [Fact]
    public async Task Execute_WithOldNotifications_ShouldCleanUp()
    {
        // Arrange — we can't easily set QueuedAt on the entity since it's set in Create().
        // Instead test with retention 0 days (all notifications are "old")
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Old", "Body", "api");
        notification.MarkSending();
        notification.MarkSent();
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        var job = new NotificationCleanupJob(_tenantProvider, _scopeFactory, NullLogger<NotificationCleanupJob>.Instance);
        var parameters = new NotificationCleanupJobParams
        {
            TenantId = "system",
            RetentionDays = 0
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var count = await _dbContext.Notifications.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Execute_WithRecentNotifications_ShouldKeep()
    {
        // Arrange
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Recent", "Body", "api");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();

        var job = new NotificationCleanupJob(_tenantProvider, _scopeFactory, NullLogger<NotificationCleanupJob>.Instance);
        var parameters = new NotificationCleanupJobParams
        {
            TenantId = "system",
            RetentionDays = 90
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var count = await _dbContext.Notifications.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Execute_EmptyDatabase_ShouldNotThrow()
    {
        // Arrange
        var job = new NotificationCleanupJob(_tenantProvider, _scopeFactory, NullLogger<NotificationCleanupJob>.Instance);
        var parameters = new NotificationCleanupJobParams
        {
            TenantId = "system",
            RetentionDays = 90
        };

        // Act & Assert
        await job.RunAsync(parameters, CancellationToken.None);
    }

    public void Dispose() => _dbContext.Dispose();
}
