using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class CreateNotificationTemplateTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateNotificationTemplateTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidTemplate_ShouldCreateTemplate()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);
        var command = new CreateNotificationTemplateCommand(
            "welcome_email", "identity", "Email", "Welcome", "<h1>Hello</h1>", "Html");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("welcome_email");
        result.Value.Channel.Should().Be("Email");
        result.Value.Format.Should().Be("Html");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeCode()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);
        var command = new CreateNotificationTemplateCommand(
            "WELCOME_EMAIL", "identity", "Email", "Welcome", "Body", "Text");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("welcome_email");
    }

    [Fact]
    public async Task Handle_DuplicateCode_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);
        var command = new CreateNotificationTemplateCommand(
            "welcome_email", "identity", "Email", "Welcome", "Body", "Html");

        await handler.Handle(command, CancellationToken.None);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_code_exists");
    }

    [Fact]
    public async Task Handle_SameCodeDifferentChannel_ShouldCreateBoth()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);

        await handler.Handle(new CreateNotificationTemplateCommand(
            "welcome", "identity", "Email", "Welcome Email", "Body", "Html"), CancellationToken.None);

        // Act
        var result = await handler.Handle(new CreateNotificationTemplateCommand(
            "welcome", "identity", "Sms", "Welcome SMS", "Body", "Text"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var count = await _dbContext.NotificationTemplates.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldPersistToDatabase()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);
        var command = new CreateNotificationTemplateCommand(
            "persist_test", "notifications", "Email", "Test", "Body", "Text");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var count = await _dbContext.NotificationTemplates.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SystemTemplate_ShouldSetIsSystem()
    {
        // Arrange
        var handler = new CreateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<CreateNotificationTemplateHandler>.Instance);
        var command = new CreateNotificationTemplateCommand(
            "system_template", "identity", "Email", "System", "Body", "Html", IsSystem: true);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSystem.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
