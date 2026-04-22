using System.Text.Json;

namespace Nexora.Modules.Contacts.Application;

/// <summary>Normalizes custom-field option input into a JSON array string.</summary>
public static class CustomFieldOptionsNormalizer
{
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
