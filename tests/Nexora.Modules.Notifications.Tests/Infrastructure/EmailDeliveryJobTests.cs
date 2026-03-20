using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.Jobs;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class EmailDeliveryJobTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public EmailDeliveryJobTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Execute_WithProvider_ShouldSendToAllRecipients()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipients(2);
        await SeedProvider();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.Sent);
        updated.Recipients.Should().AllSatisfy(r => r.Status.Should().Be(RecipientStatus.Sent));
    }

    [Fact]
    public async Task Execute_WithoutProvider_ShouldMarkFailed()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipients(1);
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.Failed);
        updated.Recipients.Should().AllSatisfy(r => r.Status.Should().Be(RecipientStatus.Failed));
    }

    [Fact]
    public async Task Execute_ExceedsDailyLimit_ShouldMarkPartialFailure()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipients(3);
        var provider = await SeedProvider(dailyLimit: 2);
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var updated = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        updated.Status.Should().Be(NotificationStatus.PartialFailure);
        updated.Recipients.Count(r => r.Status == RecipientStatus.Sent).Should().Be(2);
        updated.Recipients.Count(r => r.Status == RecipientStatus.Failed).Should().Be(1);
    }

    [Fact]
    public async Task Execute_NonExistentNotification_ShouldNotThrow()
    {
        // Arrange
        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = Guid.NewGuid()
        };

        // Act & Assert — should complete gracefully
        await job.RunAsync(parameters, CancellationToken.None);
    }

    [Fact]
    public async Task Execute_ShouldIncrementProviderSentToday()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipients(2);
        await SeedProvider();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var provider = await _dbContext.NotificationProviders.FirstAsync();
        provider.SentToday.Should().Be(2);
    }

    [Fact]
    public async Task Execute_ShouldAssignProviderMessageIds()
    {
        // Arrange
        var notification = await SeedNotificationWithRecipients(1);
        await SeedProvider();
        notification.MarkSending();
        await _dbContext.SaveChangesAsync();

        var job = new EmailDeliveryJob(_tenantAccessor, _dbContext, NullLogger<EmailDeliveryJob>.Instance);
        var parameters = new EmailDeliveryJobParams
        {
            TenantId = _tenantId.ToString(),
            NotificationId = notification.Id.Value
        };

        // Act
        await job.RunAsync(parameters, CancellationToken.None);

        // Assert
        var recipient = (await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync())
            .Recipients.First();
        recipient.ProviderMessageId.Should().NotBeNullOrEmpty();
        recipient.SentAt.Should().NotBeNull();
    }

    private async Task<Notification> SeedNotificationWithRecipients(int recipientCount)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test", "Body", "api");
        for (var i = 0; i < recipientCount; i++)
            notification.AddRecipient(Guid.NewGuid(), $"user{i}@test.com");
        notification.ClearDomainEvents();
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
        return notification;
    }

    private async Task<NotificationProvider> SeedProvider(int dailyLimit = 1000)
    {
        var provider = NotificationProvider.Create(
            _tenantId, NotificationChannel.Email, ProviderName.SendGrid, "{}", dailyLimit, isDefault: true);
        await _dbContext.NotificationProviders.AddAsync(provider);
        await _dbContext.SaveChangesAsync();
        return provider;
    }

    public void Dispose() => _dbContext.Dispose();
}
