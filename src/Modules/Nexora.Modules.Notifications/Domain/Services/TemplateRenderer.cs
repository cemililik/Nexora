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

        subject = SubstituteVariables(subject, variables);
        body = SubstituteVariables(body, variables);

        return (subject, body);
    }

    /// <summary>
    /// Renders inline content (no template) with variable substitution.
    /// </summary>
    public static string RenderInline(string content, Dictionary<string, string> variables) =>
        SubstituteVariables(content, variables);

    private static string SubstituteVariables(string content, Dictionary<string, string> variables)
    {
        if (variables.Count == 0)
            return content;

        return VariablePattern().Replace(content, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();
}
