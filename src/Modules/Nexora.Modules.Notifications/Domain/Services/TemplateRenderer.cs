using System.Net;
using System.Text.RegularExpressions;
using Nexora.Modules.Notifications.Domain.Entities;

namespace Nexora.Modules.Notifications.Domain.Services;

/// <summary>
/// Renders notification template content by substituting variables and resolving language.
/// </summary>
public static partial class TemplateRenderer
{
    /// <summary>
    /// Renders a template with variable substitution and language resolution.
    /// Falls back to the template's default language if the requested language is not available.
    /// HTML-encodes variable values in the body for HTML-format templates to prevent XSS.
    /// Subject lines are never encoded as they are plain text.
    /// </summary>
    public static (string Subject, string Body) Render(
        NotificationTemplate template,
        Dictionary<string, string> variables,
        string? languageCode = null)
    {
        var subject = template.Subject;
        var body = template.Body;

        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var translation = template.Translations
                .FirstOrDefault(t => t.LanguageCode.Equals(languageCode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (translation is not null)
            {
                subject = translation.Subject;
                body = translation.Body;
            }
        }

        var htmlEncode = template.Format == ValueObjects.TemplateFormat.Html;

        subject = SubstituteVariables(subject, variables, htmlEncode: false);
        body = SubstituteVariables(body, variables, htmlEncode);

        // Strip CR/LF from subject to prevent email header injection
        subject = subject.Replace("\r", string.Empty).Replace("\n", string.Empty);

        return (subject, body);
    }

    /// <summary>
    /// Renders inline content (no template) with variable substitution.
    /// </summary>
    /// <param name="content">The content with <c>{{variable}}</c> placeholders.</param>
    /// <param name="variables">Variable name to value mappings.</param>
    /// <param name="htmlEncode">Whether to HTML-encode variable values before substitution.</param>
    public static string RenderInline(string content, Dictionary<string, string> variables, bool htmlEncode = true) =>
        SubstituteVariables(content, variables, htmlEncode);

    private static string SubstituteVariables(string content, Dictionary<string, string> variables, bool htmlEncode = true)
    {
        if (variables.Count == 0)
            return content;

        return VariablePattern().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var value))
                return match.Value;

            return htmlEncode ? WebUtility.HtmlEncode(value) : value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();
}
