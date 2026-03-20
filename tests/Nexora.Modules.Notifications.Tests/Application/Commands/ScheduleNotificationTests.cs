using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class ScheduleNotificationTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public ScheduleNotificationTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithInlineContent_ShouldCreateSchedule()
    {
        // Arrange
        var handler = new ScheduleNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<ScheduleNotificationHandler>.Instance);
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            Subject: "Future Email", Body: "Hello!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Pending");
        result.Value.ScheduledAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(1), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_WithTemplate_ShouldCreateSchedule()
    {
        // Arrange
        await SeedTemplate("sched_welcome", "Welcome {{name}}", "Dear {{name}}!");
        var handler = new ScheduleNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<ScheduleNotificationHandler>.Instance);
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddHours(2),
            TemplateCode: "sched_welcome",
            Variables: new() { ["name"] = "John" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var notification = await _dbContext.Notifications.FirstAsync();
        notification.Subject.Should().Be("Welcome John");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ScheduleNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<ScheduleNotificationHandler>.Instance);
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            DateTime.UtcNow.AddDays(1),
            TemplateCode: "nonexistent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_ShouldPersistNotificationAndSchedule()
    {
        // Arrange
        var handler = new ScheduleNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<ScheduleNotificationHandler>.Instance);
        var command = new ScheduleNotificationCommand(
            "Sms", Guid.NewGuid(), "+1234567890",
            DateTime.UtcNow.AddHours(6),
            Subject: "SMS Scheduled", Body: "Test");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var notifications = await _dbContext.Notifications.Include(n => n.Recipients).ToListAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Recipients.Should().HaveCount(1);
        notifications[0].TriggeredBy.Should().Be("scheduled");

        var schedules = await _dbContext.NotificationSchedules.ToListAsync();
        schedules.Should().HaveCount(1);
        schedules[0].Status.Should().Be(ScheduleStatus.Pending);
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectDto()
    {
        // Arrange
        var handler = new ScheduleNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<ScheduleNotificationHandler>.Instance);
        var scheduledAt = DateTime.UtcNow.AddDays(3);
        var command = new ScheduleNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            scheduledAt,
            Subject: "Test", Body: "Body");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().NotBe(Guid.Empty);
        result.Value.NotificationId.Should().NotBe(Guid.Empty);
        result.Value.Status.Should().Be("Pending");
        result.Message!.Key.Should().Be("lockey_notifications_notification_scheduled");
    }

    private async Task SeedTemplate(string code, string subject, string body)
    {
        var template = NotificationTemplate.Create(
            _tenantId, code, "notifications", NotificationChannel.Email,
            subject, body, TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose() => _dbContext.Dispose();
}
