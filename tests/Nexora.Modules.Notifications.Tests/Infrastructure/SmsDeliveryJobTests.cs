using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class SmsDeliveryJobTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public SmsDeliveryJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Execute_WithProvider_ShouldSendSms()
    {
        // Arrange
        var notification = await SeedSmsNotification();
        await SeedSmsProvider();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new SmsDeliveryJob(_tenantAccessor, _dbContext, NullLogger<SmsDeliveryJob>.Instance);
        var parameters = new SmsDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.Sent);
        updated.Recipients.First().Status.Should().Be(RecipientStatus.Sent);
    }

    [Fact]
    public async Task Execute_WithoutProvider_ShouldMarkFailed()
    {
        // Arrange
        var notification = await SeedSmsNotification();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new SmsDeliveryJob(_tenantAccessor, _dbContext, NullLogger<SmsDeliveryJob>.Instance);
        var parameters = new SmsDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public async Task Execute_NonExistentNotification_ShouldNotThrow()
    {
        // Arrange
        var job = new SmsDeliveryJob(_tenantAccessor, _dbContext, NullLogger<SmsDeliveryJob>.Instance);
        var parameters = new SmsDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = Guid.NewGuid()
        };

        // Act & Assert
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_ShouldAssignSmsMessageId()
    {
        // Arrange
        var notification = await SeedSmsNotification();
        await SeedSmsProvider();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new SmsDeliveryJob(_tenantAccessor, _dbContext, NullLogger<SmsDeliveryJob>.Instance);
        var parameters = new SmsDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var recipient = (await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync())
            .Recipients.First();
        recipient.ProviderMessageId.Should().StartWith("sms_");
    }

    [Fact]
    public async Task Execute_ExceedsDailyLimit_ShouldFail()
    {
        // Arrange
        var notification = await SeedSmsNotification();
        var provider = await SeedSmsProvider(dailyLimit: 0);
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new SmsDeliveryJob(_tenantAccessor, _dbContext, NullLogger<SmsDeliveryJob>.Instance);
        var parameters = new SmsDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.Failed);
    }

    private async Task<Notification> SeedSmsNotification()
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Sms, "SMS Test", "Hello!", "api");
        notification.AddRecipient(Guid.NewGuid(), "+1234567890");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return notification;
    }

    private async Task<NotificationProvider> SeedSmsProvider(int dailyLimit = 1000)
    {
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Sms, ProviderName.Twilio, "{}", dailyLimit, isDefault: true);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();
        return provider;
    }

    public void Dispose() => _dbContext.Dispose();
}
