using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class DeleteNotificationTemplateTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public DeleteNotificationTemplateTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ValidTemplate_ShouldDeactivate()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new DeleteNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<DeleteNotificationTemplateHandler>.Instance);

        // Act
        var result = await handler.Handle(new DeleteNotificationTemplateCommand(template.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.NotificationTemplates.FirstAsync();
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new DeleteNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<DeleteNotificationTemplateHandler>.Instance);

        // Act
        var result = await handler.Handle(new DeleteNotificationTemplateCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_SystemTemplate_ShouldReturnFailure()
    {
        // Arrange
        var template = await SeedTemplate(isSystem: true);
        var handler = new DeleteNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<DeleteNotificationTemplateHandler>.Instance);

        // Act
        var result = await handler.Handle(new DeleteNotificationTemplateCommand(template.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_cannot_delete_system_template");
    }

    [Fact]
    public async Task Handle_AlreadyInactive_ShouldThrowDomainException()
    {
        // Arrange
        var template = await SeedTemplate();
        template.Deactivate();
        await _dbContext.SaveChangesAsync();

        var handler = new DeleteNotificationTemplateHandler(_dbContext, _tenantAccessor,
            NullLogger<DeleteNotificationTemplateHandler>.Instance);

        // Act — Deactivate() on already-inactive template throws DomainException
        var act = () => handler.Handle(new DeleteNotificationTemplateCommand(template.Id.Value), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Nexora.SharedKernel.Domain.Exceptions.DomainException>();
    }

    private async Task<NotificationTemplate> SeedTemplate(bool isSystem = false)
    {
        var template = NotificationTemplate.Create(
            _tenantId, "delete_test", "notifications", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Html, isSystem);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
