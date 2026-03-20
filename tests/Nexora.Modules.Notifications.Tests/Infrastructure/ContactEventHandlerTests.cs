using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class ContactEventHandlerTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ContactEventHandlerTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task HandleAsync_ConsentRevoked_ShouldCancelPendingSchedules()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        await SeedScheduledNotificationForContact(contactId);

        var handler = new ConsentChangedIntegrationEventHandler(
            _dbContext, NullLogger<ConsentChangedIntegrationEventHandler>.Instance);
        var @event = new ConsentChangedIntegrationEvent
        {
            TenantId = _tenantId.ToString(),
            ContactId = contactId,
            ConsentType = "EmailMarketing",
            Granted = false
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        var schedule = await _dbContext.NotificationSchedules.FirstAsync();
        schedule.Status.Should().Be(ScheduleStatus.Cancelled);
    }

    [Fact]
    public async Task HandleAsync_ConsentGranted_ShouldDoNothing()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        await SeedScheduledNotificationForContact(contactId);

        var handler = new ConsentChangedIntegrationEventHandler(
            _dbContext, NullLogger<ConsentChangedIntegrationEventHandler>.Instance);
        var @event = new ConsentChangedIntegrationEvent
        {
            TenantId = _tenantId.ToString(),
            ContactId = contactId,
            ConsentType = "EmailMarketing",
            Granted = true
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        var schedule = await _dbContext.NotificationSchedules.FirstAsync();
        schedule.Status.Should().Be(ScheduleStatus.Pending);
    }

    [Fact]
    public async Task HandleAsync_NoSchedules_ShouldNotThrow()
    {
        // Arrange
        var handler = new ConsentChangedIntegrationEventHandler(
            _dbContext, NullLogger<ConsentChangedIntegrationEventHandler>.Instance);
        var @event = new ConsentChangedIntegrationEvent
        {
            TenantId = _tenantId.ToString(),
            ContactId = Guid.NewGuid(),
            ConsentType = "SmsMarketing",
            Granted = false
        };

        // Act & Assert — should not throw
        await handler.HandleAsync(@event, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_DifferentContact_ShouldNotCancelOtherSchedules()
    {
        // Arrange
        var contactId = Guid.NewGuid();
        var otherContactId = Guid.NewGuid();
        await SeedScheduledNotificationForContact(contactId);

        var handler = new ConsentChangedIntegrationEventHandler(
            _dbContext, NullLogger<ConsentChangedIntegrationEventHandler>.Instance);
        var @event = new ConsentChangedIntegrationEvent
        {
            TenantId = _tenantId.ToString(),
            ContactId = otherContactId,
            ConsentType = "EmailMarketing",
            Granted = false
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        var schedule = await _dbContext.NotificationSchedules.FirstAsync();
        schedule.Status.Should().Be(ScheduleStatus.Pending);
    }

    private async Task SeedScheduledNotificationForContact(Guid contactId)
    {
        var notification = Notification.Create(
            _tenantId, NotificationChannel.Email, "Test", "Body", "scheduled");
        notification.AddRecipient(contactId, "user@test.com");
        notification.ClearDomainEvents();
        var schedule = NotificationSchedule.Create(notification.Id, DateTime.UtcNow.AddDays(1));
        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.NotificationSchedules.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
