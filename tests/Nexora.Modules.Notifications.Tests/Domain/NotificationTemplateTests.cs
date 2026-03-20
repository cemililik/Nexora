using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationTemplateTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_ReturnsTemplate()
    {
        // Arrange & Act
        var template = NotificationTemplate.Create(
            _tenantId, "welcome_email", "identity", NotificationChannel.Email,
            "Welcome!", "<p>Hello {name}</p>", TemplateFormat.Html);

        // Assert
        template.Id.Value.Should().NotBeEmpty();
        template.TenantId.Should().Be(_tenantId);
        template.Code.Should().Be("welcome_email");
        template.Module.Should().Be("identity");
        template.Channel.Should().Be(NotificationChannel.Email);
        template.Subject.Should().Be("Welcome!");
        template.Body.Should().Be("<p>Hello {name}</p>");
        template.Format.Should().Be(TemplateFormat.Html);
        template.IsActive.Should().BeTrue();
        template.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void Create_NormalizesCodeToLowercase()
    {
        // Arrange & Act
        var template = NotificationTemplate.Create(
            _tenantId, "WELCOME_EMAIL", "Identity", NotificationChannel.Email,
            "Welcome!", "Hello", TemplateFormat.Text);

        // Assert
        template.Code.Should().Be("welcome_email");
        template.Module.Should().Be("identity");
    }

    [Fact]
    public void Create_RaisesTemplateCreatedEvent()
    {
        // Arrange & Act
        var template = NotificationTemplate.Create(
            _tenantId, "test_code", "crm", NotificationChannel.Sms,
            "Test", "Body", TemplateFormat.Text);

        // Assert
        template.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TemplateCreatedEvent>()
            .Which.Code.Should().Be("test_code");
    }

    [Fact]
    public void Create_WithSystemFlag_SetsIsSystem()
    {
        // Arrange & Act
        var template = NotificationTemplate.Create(
            _tenantId, "system_welcome", "identity", NotificationChannel.Email,
            "Welcome!", "Hello", TemplateFormat.Html, isSystem: true);

        // Assert
        template.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Update_WithNonSystemTemplate_UpdatesFields()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Old Subject", "Old Body", TemplateFormat.Text);
        template.ClearDomainEvents();

        // Act
        template.Update("New Subject", "New Body", TemplateFormat.Html);

        // Assert
        template.Subject.Should().Be("New Subject");
        template.Body.Should().Be("New Body");
        template.Format.Should().Be(TemplateFormat.Html);
        template.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TemplateUpdatedEvent>();
    }

    [Fact]
    public void Update_SystemTemplate_ThrowsDomainException()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "sys", "identity", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text, isSystem: true);

        // Act
        var act = () => template.Update("New", "New", TemplateFormat.Html);

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_cannot_edit_system_template");
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ThrowsDomainException()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);

        // Act
        var act = () => template.Activate();

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_template_already_active");
    }

    [Fact]
    public void Deactivate_WhenActive_SetsInactive()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);

        // Act
        template.Deactivate();

        // Assert
        template.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AddTranslation_WithNewLanguage_AddsTranslation()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);

        // Act
        template.AddTranslation("tr", "Konu", "İçerik");

        // Assert
        template.Translations.Should().HaveCount(1);
        template.Translations[0].LanguageCode.Should().Be("tr");
        template.Translations[0].Subject.Should().Be("Konu");
    }

    [Fact]
    public void AddTranslation_WithExistingLanguage_ThrowsDomainException()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);
        template.AddTranslation("tr", "Konu", "İçerik");

        // Act
        var act = () => template.AddTranslation("tr", "New", "New");

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_translation_already_exists");
    }

    [Fact]
    public void UpdateTranslation_WithExistingLanguage_UpdatesContent()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);
        template.AddTranslation("tr", "Eski Konu", "Eski İçerik");

        // Act
        template.UpdateTranslation("tr", "Yeni Konu", "Yeni İçerik");

        // Assert
        template.Translations[0].Subject.Should().Be("Yeni Konu");
        template.Translations[0].Body.Should().Be("Yeni İçerik");
    }

    [Fact]
    public void UpdateTranslation_WithNonExistentLanguage_ThrowsDomainException()
    {
        // Arrange
        var template = NotificationTemplate.Create(
            _tenantId, "test", "crm", NotificationChannel.Email,
            "Subject", "Body", TemplateFormat.Text);

        // Act
        var act = () => template.UpdateTranslation("fr", "Sujet", "Corps");

        // Assert
        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_notifications_error_translation_not_found");
    }
}
