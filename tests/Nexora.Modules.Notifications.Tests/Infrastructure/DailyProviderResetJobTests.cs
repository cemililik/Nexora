using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DailyProviderResetJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
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

        var job = new DailyProviderResetJob(_tenantAccessor, _dbContext, NullLogger<DailyProviderResetJob>.Instance);
        var parameters = new DailyProviderResetJobParams { TenantId = _tenantId.ToString() };

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

        var job = new DailyProviderResetJob(_tenantAccessor, _dbContext, NullLogger<DailyProviderResetJob>.Instance);
        var parameters = new DailyProviderResetJobParams { TenantId = _tenantId.ToString() };

        // Act & Assert — should complete without error
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_ShouldOnlyResetCurrentTenant()
    {
        // Arrange
        var ownProvider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", 1000);
        ownProvider.IncrementSentToday(25);

        var otherProvider = NotificationProvider.Create(
            Guid.NewGuid(), NotificationChannel.Email, ProviderName.Mailgun, "{}", 500);
        otherProvider.IncrementSentToday(10);

        await _dbContext.NotificationProviders.AddRangeAsync(ownProvider, otherProvider);
        await _dbContext.SaveChangesAsync();

        var job = new DailyProviderResetJob(_tenantAccessor, _dbContext, NullLogger<DailyProviderResetJob>.Instance);
        var parameters = new DailyProviderResetJobParams { TenantId = _tenantId.ToString() };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var other = await _dbContext.NotificationProviders.FirstAsync(p => p.TenantId != _tenantId);
        other.SentToday.Should().Be(10); // Unchanged
    }

    public void Dispose() => _dbContext.Dispose();
}
