using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class SendNotificationTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public SendNotificationTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_WithTemplate_ShouldCreateNotification()
    {
        // Arrange
        await SeedTemplate("welcome", "Welcome {{name}}", "Hello {{name}}, welcome!");
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "welcome",
            Variables: new() { ["name"] = "John" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Welcome John");
        result.Value.Channel.Should().Be("Email");
        result.Value.TotalRecipients.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithInlineContent_ShouldCreateNotification()
    {
        // Arrange
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            Subject: "Inline Subject",
            Body: "Hello {{name}}!",
            Variables: new() { ["name"] = "Jane" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Inline Subject");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "nonexistent");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_WithTranslation_ShouldUseTranslatedContent()
    {
        // Arrange
        var template = await SeedTemplate("greet", "Greeting", "Hello World");
        template.AddTranslation("tr", "Selamlama", "Merhaba Dünya");
        await _dbContext.SaveChangesAsync();

        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "greet", LanguageCode: "tr");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Selamlama");
    }

    [Fact]
    public async Task Handle_ShouldPersistNotificationAndRecipient()
    {
        // Arrange
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Sms", Guid.NewGuid(), "+1234567890",
            Subject: "SMS Test", Body: "Test body");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var notifications = await _dbContext.Notifications
            .Include(n => n.Recipients)
            .ToListAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Recipients.Should().HaveCount(1);
        notifications[0].Status.Should().Be(NotificationStatus.Sending);
    }

    [Fact]
    public async Task Handle_InactiveTemplate_ShouldReturnFailure()
    {
        // Arrange
        var template = await SeedTemplate("inactive", "Subject", "Body");
        template.Deactivate();
        await _dbContext.SaveChangesAsync();

        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "inactive");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_VariableSubstitution_ShouldRenderAllVariables()
    {
        // Arrange
        await SeedTemplate("multi_var", "Order {{orderId}}", "Dear {{name}}, your order {{orderId}} is {{status}}.");
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "multi_var",
            Variables: new() { ["name"] = "Alice", ["orderId"] = "12345", ["status"] = "shipped" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Order 12345");
    }

    [Fact]
    public async Task Handle_MissingVariables_ShouldKeepPlaceholders()
    {
        // Arrange
        await SeedTemplate("missing_var", "Hello {{name}}", "{{greeting}} {{name}}");
        var handler = new SendNotificationHandler(_dbContext, _tenantAccessor,
            NullLogger<SendNotificationHandler>.Instance);
        var command = new SendNotificationCommand(
            "Email", Guid.NewGuid(), "user@test.com",
            TemplateCode: "missing_var",
            Variables: new() { ["name"] = "Bob" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Hello Bob");
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
