using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class SendBulkNotificationTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public SendBulkNotificationTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithInlineContent_ShouldCreateBulkNotification()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com"), new BulkRecipient(Guid.NewGuid(), "b@test.com")],
            Subject: "Bulk Test", Body: "Hello everyone!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalRecipients.Should().Be(2);
        result.Value.QueuedCount.Should().Be(2);
        result.Value.SkippedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithTemplate_ShouldCreateBulkNotification()
    {
        // Arrange
        await SeedTemplate("bulk_welcome", "Welcome", "Hello all!");
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            TemplateCode: "bulk_welcome");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueuedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            TemplateCode: "nonexistent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_EmptyAddresses_ShouldSkipInvalid()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [
                new BulkRecipient(Guid.NewGuid(), "valid@test.com"),
                new BulkRecipient(Guid.NewGuid(), ""),
                new BulkRecipient(Guid.NewGuid(), "  ")
            ],
            Subject: "Test", Body: "Body");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QueuedCount.Should().Be(1);
        result.Value.SkippedCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AllEmptyAddresses_ShouldReturnFailure()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), ""), new BulkRecipient(Guid.NewGuid(), "  ")],
            Subject: "Test", Body: "Body");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_bulk_no_valid_recipients");
    }

    [Fact]
    public async Task Handle_ShouldPersistNotificationWithRecipients()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Sms",
            [new BulkRecipient(Guid.NewGuid(), "+1111"), new BulkRecipient(Guid.NewGuid(), "+2222")],
            Subject: "Test", Body: "Body");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var notification = await _dbContext.Notifications.Include(n => n.Recipients).FirstAsync();
        notification.TotalRecipients.Should().Be(2);
        notification.Recipients.Should().HaveCount(2);
        notification.TriggeredBy.Should().Be("bulk_api");
    }

    [Fact]
    public async Task Handle_WithVariables_ShouldRenderTemplate()
    {
        // Arrange
        await SeedTemplate("bulk_var", "Hello {{name}}", "Dear {{name}}, welcome!");
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            TemplateCode: "bulk_var",
            Variables: new() { ["name"] = "Team" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var notification = await _dbContext.Notifications.FirstAsync();
        notification.Subject.Should().Be("Hello Team");
    }

    [Fact]
    public async Task Handle_InlineContent_ShouldRenderVariables()
    {
        // Arrange
        var handler = new SendBulkNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendBulkNotificationHandler>.Instance);
        var command = new SendBulkNotificationCommand(
            "Email",
            [new BulkRecipient(Guid.NewGuid(), "a@test.com")],
            Subject: "Update", Body: "Hi {{name}}, update ready.",
            Variables: new() { ["name"] = "Admin" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var notification = await _dbContext.Notifications.FirstAsync();
        notification.BodyRendered.Should().Be("Hi Admin, update ready.");
    }

    private async Task<NotificationTemplate> SeedTemplate(string code, string subject, string body)
    {
        var template = NotificationTemplate.Create(
            _tenantId, code, "notifications", NotificationChannel.Email,
            subject, body, TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
