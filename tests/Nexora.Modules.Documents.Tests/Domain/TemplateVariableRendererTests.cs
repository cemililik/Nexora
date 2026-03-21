using Nexora.Modules.Documents.Domain.Services;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Tests.Domain;

public sealed class TemplateVariableRendererTests
{
    [Fact]
    public void Render_SimpleSubstitution_ReplacesVariables()
    {
        var content = "Hello {{name}}, welcome to {{company}}!";
        var variables = new Dictionary<string, string>
        {
            { "name", "John" },
            { "company", "Acme Corp" }
        };

        var result = TemplateVariableRenderer.Render(content, variables);

        result.Should().Be("Hello John, welcome to Acme Corp!");
    }

    [Fact]
    public void Render_WithHtmlChars_EscapesChars()
    {
        var content = "Value: {{value}}";
        var variables = new Dictionary<string, string> { { "value", "<script>alert('xss')</script>" } };

        var result = TemplateVariableRenderer.Render(content, variables);

        result.Should().Contain("&lt;script&gt;");
        result.Should().NotContain("<script>");
    }

    [Fact]
    public void Render_UnmatchedVariable_KeepsPlaceholder()
    {
        var content = "Hello {{name}}, your ID is {{id}}";
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var result = TemplateVariableRenderer.Render(content, variables);

        result.Should().Be("Hello John, your ID is {{id}}");
    }

    [Fact]
    public void Render_EmptyContent_ThrowsException()
    {
        var act = () => TemplateVariableRenderer.Render("", new Dictionary<string, string>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Render_RequiredVariableMissing_ThrowsException()
    {
        var varDefs = """{"name": {"required": true}}""";
        var variables = new Dictionary<string, string>();

        var act = () => TemplateVariableRenderer.Render("Hello {{name}}", variables, varDefs);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Render_RequiredVariablePresent_DoesNotThrow()
    {
        var varDefs = """{"name": {"required": true}}""";
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var result = TemplateVariableRenderer.Render("Hello {{name}}", variables, varDefs);

        result.Should().Be("Hello John");
    }

    [Fact]
    public void Render_SpacesInPlaceholder_HandlesCorrectly()
    {
        var content = "Hello {{ name }}";
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var result = TemplateVariableRenderer.Render(content, variables);

        result.Should().Be("Hello John");
    }

    [Fact]
    public void ExtractVariables_DuplicateNames_ReturnsDistinctNames()
    {
        var content = "{{name}} and {{email}} and {{name}}";

        var variables = TemplateVariableRenderer.ExtractVariables(content);

        variables.Should().HaveCount(2);
        variables.Should().Contain("name");
        variables.Should().Contain("email");
    }

    [Fact]
    public void ExtractVariables_EmptyContent_ReturnsEmpty()
    {
        var variables = TemplateVariableRenderer.ExtractVariables("");

        variables.Should().BeEmpty();
    }

    [Fact]
    public void ValidateVariables_InvalidJsonDefinitions_ThrowsException()
    {
        var invalidJson = "not valid json {{{";
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var act = () => TemplateVariableRenderer.ValidateVariables(variables, invalidJson);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_documents_error_template_variable_definitions_invalid");
    }

    [Fact]
    public void ValidateVariables_NullDefinitions_DoesNotThrow()
    {
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var act = () => TemplateVariableRenderer.ValidateVariables(variables, null);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateVariables_RequiredMissing_ThrowsException()
    {
        var varDefs = """{"name": {"required": true}, "optional": {"required": false}}""";
        var variables = new Dictionary<string, string> { { "optional", "value" } };

        var act = () => TemplateVariableRenderer.ValidateVariables(variables, varDefs);

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_documents_error_template_variable_required");
    }

    [Fact]
    public void ValidateVariables_AllRequiredPresent_DoesNotThrow()
    {
        var varDefs = """{"name": {"required": true}}""";
        var variables = new Dictionary<string, string> { { "name", "John" } };

        var act = () => TemplateVariableRenderer.ValidateVariables(variables, varDefs);

        act.Should().NotThrow();
    }

    [Fact]
    public void Render_InvalidJsonDefinitions_ThrowsException()
    {
        var act = () => TemplateVariableRenderer.Render("Hello {{name}}", new Dictionary<string, string> { { "name", "John" } }, "bad json");

        act.Should().Throw<DomainException>()
            .Which.LocalizationKey.Should().Be("lockey_documents_error_template_variable_definitions_invalid");
    }
}
