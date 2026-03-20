using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class UpdateNotificationTemplateTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public UpdateNotificationTemplateTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ShouldUpdateTemplate()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new UpdateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationTemplateHandler>.Instance);
        var command = new UpdateNotificationTemplateCommand(template.Id.Value, "New Subject", "New Body", "Text");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("New Subject");
        result.Value.Format.Should().Be("Text");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationTemplateHandler>.Instance);
        var command = new UpdateNotificationTemplateCommand(Guid.NewGuid(), "Subject", "Body", "Html");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_SystemTemplate_ShouldThrowDomainException()
    {
        // Arrange
        var template = await SeedTemplate(isSystem: true);
        var handler = new UpdateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationTemplateHandler>.Instance);
        var command = new UpdateNotificationTemplateCommand(template.Id.Value, "Updated", "Body", "Html");

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert — DomainException is thrown because Update() rejects system templates
        await act.Should().ThrowAsync<Nexora.SharedKernel.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            Guid.NewGuid(), "other_tenant_template", "identity", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Html);
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationTemplateHandler>.Instance);
        var command = new UpdateNotificationTemplateCommand(template.Id.Value, "Updated", "Body", "Html");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldPersistChanges()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new UpdateNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<UpdateNotificationTemplateHandler>.Instance);

        // Act
        await handler.Handle(
            new UpdateNotificationTemplateCommand(template.Id.Value, "Persisted Subject", "Persisted Body", "Markdown"),
            CancellationToken.None);

        // Assert
        var updated = await _dbContext.NotificationTemplates.FirstAsync();
        updated.Subject.Should().Be("Persisted Subject");
        updated.Format.Should().Be(TemplateFormat.Markdown);
    }

    private async Task<NotificationTemplate> SeedTemplate(bool isSystem = false)
    {
        var template = NotificationTemplate.Create(
            _tenantId, "test_template", "notifications", NotificationChannel.Email,
            "Original Subject", "Original Body", TemplateFormat.Html, isSystem);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
