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

public sealed class DailyProviderResetJobTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IActiveTenantProvider _tenantProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DailyProviderResetJobTests()
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
    public async Task Execute_WithNonZeroCounters_ShouldResetAll()
    {
        // Arrange
        var provider1 = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", 1000);
        provider1.IncrementSentToday(50);

        var provider2 = NotificationProvider.Create(
            _tenantId, NotificationChannel.Sms, ProviderName.Twilio, "{}", 500);
        provider2.IncrementSentToday(30);

        await _dbContext.NotificationProviders.AddRangeAsync(provider1, provider2);
        await _dbContext.SaveChangesAsync();

        var job = new DailyProviderResetJob(_tenantProvider, _scopeFactory, NullLogger<DailyProviderResetJob>.Instance);
        var parameters = new DailyProviderResetJobParams { TenantId = "system" };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var providers = await _dbContext.NotificationProviders.ToListAsync();
        providers.Should().AllSatisfy(p => p.SentToday.Should().Be(0));
    }

    [Fact]
    public async Task Execute_WithZeroCounters_ShouldDoNothing()
    {
        // Arrange
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", 1000);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();

        var job = new DailyProviderResetJob(_tenantProvider, _scopeFactory, NullLogger<DailyProviderResetJob>.Instance);
        var parameters = new DailyProviderResetJobParams { TenantId = "system" };

        // Act & Assert — should complete without error
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_AllProviders_AreResetByPlatformJob()
    {
        // Note: Tenant schema isolation cannot be tested with in-memory DB.
        // PlatformJob creates a fresh DI scope per tenant in production.
        // This test verifies all providers with SentToday > 0 are reset.
        var provider1 = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", 1000);
        provider1.IncrementSentToday(25);

        var provider2 = NotificationProvider.Create(
            Guid.NewGuid(), NotificationChannel.Email, ProviderName.Mailgun, "{}", 500);
        provider2.IncrementSentToday(10);

        await _dbContext.NotificationProviders.AddRangeAsync(provider1, provider2);
        await _dbContext.SaveChangesAsync();

        var job = new DailyProviderResetJob(_tenantProvider, _scopeFactory, NullLogger<DailyProviderResetJob>.Instance);

        // Act
        await job.RunAsync(new DailyProviderResetJobParams { TenantId = "system" }, CancellationToken.None);

        // Assert — in-memory DB has no schema isolation, both are reset
        var providers = await _dbContext.NotificationProviders.ToListAsync();
        providers.Should().AllSatisfy(p => p.SentToday.Should().Be(0));
    }

    public void Dispose() => _dbContext.Dispose();
}
