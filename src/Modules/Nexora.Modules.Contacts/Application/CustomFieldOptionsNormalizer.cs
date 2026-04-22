using System.Text.Json;

namespace Nexora.Modules.Contacts.Application;

/// <summary>Normalizes custom-field option input into a canonical JSON array string.</summary>
public static class CustomFieldOptionsNormalizer
{
    /// <summary>
    /// Converts <paramref name="options"/> into a deduplicated, trimmed JSON array string.
    /// Accepts either an existing JSON array or a delimiter-separated list (comma, newline, carriage-return).
    /// </summary>
    /// <param name="options">Raw options string, or <see langword="null"/> / whitespace-only.</param>
    /// <returns>
    /// A JSON array string (e.g. <c>["A","B"]</c>), or <see langword="null"/> when
    /// <paramref name="options"/> is <see langword="null"/> or whitespace-only.
    /// </returns>
    public static string? Normalize(string? options)
    {
        if (string.IsNullOrWhiteSpace(options))
            return null;

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(options);
            if (parsed is not null)
            {
                var cleaned = parsed
                    .Select(s => s?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToArray();
                return JsonSerializer.Serialize(cleaned);
            }
        }
        catch (JsonException)
        {
            // Not a JSON array — fall through to delimiter-based fallback.
        }

        var items = options
            .Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToArray();

        return JsonSerializer.Serialize(items);
    }
}
