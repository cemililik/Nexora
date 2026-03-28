using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.Services;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_WithVariables_ShouldSubstitute()
    {
        // Arrange
        var template = CreateTemplate("Hello {{name}}", "Dear {{name}}, welcome to {{company}}!");
        var variables = new Dictionary<string, string>
        {
            ["name"] = "John",
            ["company"] = "Nexora"
        };

        // Act
        var (subject, body) = TemplateRenderer.Render(template, variables);

        // Assert
        subject.Should().Be("Hello John");
        body.Should().Be("Dear John, welcome to Nexora!");
    }

    [Fact]
    public void Render_WithoutVariables_ShouldReturnOriginal()
    {
        // Arrange
        var template = CreateTemplate("Static Subject", "Static Body");

        // Act
        var (subject, body) = TemplateRenderer.Render(template, new());

        // Assert
        subject.Should().Be("Static Subject");
        body.Should().Be("Static Body");
    }

    [Fact]
    public void Render_MissingVariable_ShouldKeepPlaceholder()
    {
        // Arrange
        var template = CreateTemplate("Hello {{name}}", "Welcome {{unknown}}");
        var variables = new Dictionary<string, string> { ["name"] = "Alice" };

        // Act
        var (subject, body) = TemplateRenderer.Render(template, variables);

        // Assert
        subject.Should().Be("Hello Alice");
        body.Should().Be("Welcome {{unknown}}");
    }

    [Fact]
    public void Render_WithLanguage_ShouldUseTranslation()
    {
        // Arrange
        var template = CreateTemplate("English Subject", "English Body");
        template.AddTranslation("tr", "Türkçe Konu", "Türkçe {{name}}");
        var variables = new Dictionary<string, string> { ["name"] = "Mehmet" };

        // Act
        var (subject, body) = TemplateRenderer.Render(template, variables, "tr");

        // Assert
        subject.Should().Be("Türkçe Konu");
        body.Should().Be("Türkçe Mehmet");
    }

    [Fact]
    public void Render_WithUnavailableLanguage_ShouldFallbackToDefault()
    {
        // Arrange
        var template = CreateTemplate("Default Subject", "Default Body");

        // Act
        var (subject, body) = TemplateRenderer.Render(template, new(), "fr");

        // Assert
        subject.Should().Be("Default Subject");
        body.Should().Be("Default Body");
    }

    [Fact]
    public void Render_NullLanguage_ShouldUseDefault()
    {
        // Arrange
        var template = CreateTemplate("Default", "Body");
        template.AddTranslation("tr", "Türkçe", "Gövde");

        // Act
        var (subject, _) = TemplateRenderer.Render(template, new(), null);

        // Assert
        subject.Should().Be("Default");
    }

    [Fact]
    public void RenderInline_WithVariables_ShouldSubstitute()
    {
        // Arrange
        var content = "Hello {{name}}, your order {{orderId}} is ready.";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Jane",
            ["orderId"] = "ORD-001"
        };

        // Act
        var result = TemplateRenderer.RenderInline(content, variables, htmlEncode: false);

        // Assert
        result.Should().Be("Hello Jane, your order ORD-001 is ready.");
    }

    [Fact]
    public void RenderInline_EmptyVariables_ShouldReturnOriginal()
    {
        // Arrange
        var content = "No variables here.";

        // Act
        var result = TemplateRenderer.RenderInline(content, new(), htmlEncode: false);

        // Assert
        result.Should().Be("No variables here.");
    }

    private static NotificationTemplate CreateTemplate(string subject, string body)
    {
        return NotificationTemplate.Create(
            Guid.NewGuid(), "test_template", "notifications",
            NotificationChannel.Email, subject, body, TemplateFormat.Html);
    }
}
