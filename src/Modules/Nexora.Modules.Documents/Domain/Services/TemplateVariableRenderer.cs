using System.Text.Json;
using System.Text.RegularExpressions;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Services;

/// <summary>
/// Renders template content by substituting <c>{{variable}}</c> placeholders with provided values.
/// Validates required variables and applies HTML escaping for safety.
/// </summary>
public static partial class TemplateVariableRenderer
{
    private static readonly Regex VariablePattern = MyRegex();

    /// <summary>Renders template content by replacing variable placeholders with provided values.</summary>
    /// <param name="templateContent">Template content with <c>{{variable}}</c> placeholders.</param>
    /// <param name="variables">Dictionary of variable name to value mappings.</param>
    /// <param name="variableDefinitions">Optional JSON variable definitions for required validation.</param>
    /// <returns>Rendered content with all variables substituted.</returns>
    public static string Render(string templateContent, Dictionary<string, string> variables, string? variableDefinitions = null)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
            throw new DomainException("lockey_documents_error_template_content_empty");

        ValidateVariables(variables, variableDefinitions);

        return VariablePattern.Replace(templateContent, match =>
        {
            var variableName = match.Groups[1].Value.Trim();
            return variables.TryGetValue(variableName, out var value)
                ? EscapeHtml(value)
                : match.Value; // Keep unmatched placeholders as-is
        });
    }

    /// <summary>Validates provided variables against definitions without rendering.</summary>
    /// <param name="variables">Dictionary of variable name to value mappings.</param>
    /// <param name="variableDefinitions">Optional JSON variable definitions for required validation.</param>
    /// <exception cref="DomainException">Thrown if required variables are missing or definitions are invalid.</exception>
    public static void ValidateVariables(Dictionary<string, string> variables, string? variableDefinitions)
    {
        if (string.IsNullOrWhiteSpace(variableDefinitions))
            return;

        try
        {
            using var doc = JsonDocument.Parse(variableDefinitions);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("required", out var reqProp) && reqProp.GetBoolean())
                {
                    if (!variables.ContainsKey(prop.Name) || string.IsNullOrWhiteSpace(variables[prop.Name]))
                        throw new DomainException("lockey_documents_error_template_variable_required");
                }
            }
        }
        catch (JsonException)
        {
            throw new DomainException("lockey_documents_error_template_variable_definitions_invalid");
        }
    }

    /// <summary>Extracts all variable names from template content.</summary>
    /// <param name="templateContent">Template content to scan.</param>
    /// <returns>Distinct variable names found in the template.</returns>
    public static IReadOnlyList<string> ExtractVariables(string templateContent)
    {
        if (string.IsNullOrWhiteSpace(templateContent))
            return [];

        return VariablePattern.Matches(templateContent)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string EscapeHtml(string value) =>
        value.Replace("&", "&amp;")
             .Replace("<", "&lt;")
             .Replace(">", "&gt;")
             .Replace("\"", "&quot;")
             .Replace("'", "&#39;");

    [GeneratedRegex(@"\{\{(\s*\w+\s*)\}\}")]
    private static partial Regex MyRegex();
}
