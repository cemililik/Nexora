using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Queries;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Queries;

public sealed class GetNotificationTemplateByIdTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public GetNotificationTemplateByIdTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingTemplate_ShouldReturnDetail()
    {
        // Arrange
        var template = await SeedTemplateWithTranslation();
        var handler = new GetNotificationTemplateByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationTemplateByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationTemplateByIdQuery(template.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("detail_test");
        result.Value.Body.Should().Be("English Body");
        result.Value.Translations.Should().HaveCount(1);
        result.Value.Translations[0].LanguageCode.Should().Be("tr");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new GetNotificationTemplateByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationTemplateByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationTemplateByIdQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_DifferentTenant_ShouldReturnFailure()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            Guid.NewGuid(), "other_tenant", "notifications", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationTemplateByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationTemplateByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationTemplateByIdQuery(template.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_TemplateWithNoTranslations_ShouldReturnEmptyList()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "no_translations", "notifications", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();

        var handler = new GetNotificationTemplateByIdHandler(_dbContext, _tenantAccessor,
            NullLogger<GetNotificationTemplateByIdHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetNotificationTemplateByIdQuery(template.Id.Value), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Translations.Should().BeEmpty();
    }

    private async Task<NotificationTemplate> SeedTemplateWithTranslation()
    {
        var template = NotificationTemplate.Create(
            _tenantId, "detail_test", "notifications", NotificationChannel.Email,
            "English Subject", "English Body", TemplateFormat.Html);
        template.AddTranslation("tr", "Türkçe Konu", "Türkçe Gövde");
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
