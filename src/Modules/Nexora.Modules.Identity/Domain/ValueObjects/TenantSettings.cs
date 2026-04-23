using System.Text.Json;

namespace Nexora.Modules.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed tenant-level locale and regional settings, stored as JSONB in Tenant.Settings.
/// Deserialization failures silently fall back to <see cref="Default"/> — intentional safe behavior
/// for rows created before this feature was introduced.
/// </summary>
public sealed record TenantSettings(
    string DefaultLocale,
    string DefaultCurrency,
    string DefaultTimezone,
    string DefaultDocumentLanguage)
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Platform-level defaults used when no tenant settings are configured.</summary>
    public static TenantSettings Default => new("en-US", "USD", "UTC", "en");

    /// <summary>
    /// Deserializes a <see cref="TenantSettings"/> from the JSON stored in the Tenant.Settings column.
    /// Returns <see cref="Default"/> on null input or any parse failure.
    /// </summary>
    public static TenantSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default;

        try
        {
            var parsed = JsonSerializer.Deserialize<TenantSettings>(json, _jsonOptions);
            if (parsed is null)
                return Default;

            // A syntactically-valid but semantically-empty payload (e.g. "{}") deserializes with
            // null/empty fields. Fall back to platform defaults for any missing field so locale
            // resolution never returns blank values.
            return new TenantSettings(
                DefaultLocale: string.IsNullOrWhiteSpace(parsed.DefaultLocale) ? Default.DefaultLocale : parsed.DefaultLocale,
                DefaultCurrency: string.IsNullOrWhiteSpace(parsed.DefaultCurrency) ? Default.DefaultCurrency : parsed.DefaultCurrency,
                DefaultTimezone: string.IsNullOrWhiteSpace(parsed.DefaultTimezone) ? Default.DefaultTimezone : parsed.DefaultTimezone,
                DefaultDocumentLanguage: string.IsNullOrWhiteSpace(parsed.DefaultDocumentLanguage) ? Default.DefaultDocumentLanguage : parsed.DefaultDocumentLanguage);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>Serializes this instance to JSON for storage in the Tenant.Settings column.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);
}
