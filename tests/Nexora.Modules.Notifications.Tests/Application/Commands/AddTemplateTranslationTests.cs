using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.Modules.Notifications.Tests.Helpers;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Tests.Application.Commands;

public sealed class AddTemplateTranslationTests : IDisposable
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AddTemplateTranslationTests()
    {
        _tenantAccessor = TestTenantAccessor.Create(_tenantId, _orgId);
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new NotificationsDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NewTranslation_ShouldAddTranslation()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new AddTemplateTranslationHandler(_dbContext, _tenantAccessor,
            NullLogger<AddTemplateTranslationHandler>.Instance);
        var command = new AddTemplateTranslationCommand(template.Id.Value, "tr", "Hoşgeldiniz", "Merhaba");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.LanguageCode.Should().Be("tr");
        result.Value.Subject.Should().Be("Hoşgeldiniz");
        result.Message!.Key.Should().Be("lockey_notifications_translation_added");
    }

    [Fact]
    public async Task Handle_ExistingTranslation_ShouldUpdateTranslation()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new AddTemplateTranslationHandler(_dbContext, _tenantAccessor,
            NullLogger<AddTemplateTranslationHandler>.Instance);

        await handler.Handle(
            new AddTemplateTranslationCommand(template.Id.Value, "tr", "Eski Konu", "Eski Gövde"),
            CancellationToken.None);

        // Act
        var result = await handler.Handle(
            new AddTemplateTranslationCommand(template.Id.Value, "tr", "Yeni Konu", "Yeni Gövde"),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("Yeni Konu");
        result.Message!.Key.Should().Be("lockey_notifications_translation_updated");
    }

    [Fact]
    public async Task Handle_NonExistentTemplate_ShouldReturnFailure()
    {
        // Arrange
        var handler = new AddTemplateTranslationHandler(_dbContext, _tenantAccessor,
            NullLogger<AddTemplateTranslationHandler>.Instance);
        var command = new AddTemplateTranslationCommand(Guid.NewGuid(), "en", "Subject", "Body");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Key.Should().Be("lockey_notifications_error_template_not_found");
    }

    [Fact]
    public async Task Handle_ShouldNormalizeLanguageCode()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new AddTemplateTranslationHandler(_dbContext, _tenantAccessor,
            NullLogger<AddTemplateTranslationHandler>.Instance);
        var command = new AddTemplateTranslationCommand(template.Id.Value, "TR", "Konu", "Gövde");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.LanguageCode.Should().Be("tr");
    }

    [Fact]
    public async Task Handle_MultipleLanguages_ShouldAddAll()
    {
        // Arrange
        var template = await SeedTemplate();
        var handler = new AddTemplateTranslationHandler(_dbContext, _tenantAccessor,
            NullLogger<AddTemplateTranslationHandler>.Instance);

        // Act
        await handler.Handle(new AddTemplateTranslationCommand(template.Id.Value, "tr", "Konu", "Gövde"), CancellationToken.None);
        await handler.Handle(new AddTemplateTranslationCommand(template.Id.Value, "de", "Betreff", "Körper"), CancellationToken.None);

        // Assert
        var saved = await _dbContext.NotificationTemplates
            .Include(t => t.Translations)
            .FirstAsync(t => t.Id == template.Id);
        saved.Translations.Should().HaveCount(2);
    }

    private async Task<NotificationTemplate> SeedTemplate()
    {
        var template = NotificationTemplate.Create(
            _tenantId, "translation_test", "notifications", NotificationChannel.Email,
            "English Subject", "English Body", TemplateFormat.Html);
        template.ClearDomainEvents();
        await _dbContext.NotificationTemplates.AddAsync(template);
        await _dbContext.SaveChangesAsync();
        return template;
    }

    public void Dispose() => _dbContext.Dispose();
}
