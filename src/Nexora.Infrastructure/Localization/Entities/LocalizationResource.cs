namespace Nexora.Infrastructure.Localization.Entities;

/// <summary>
/// Base translation resource shared across all tenants (public schema).
/// </summary>
public sealed class LocalizationResource
{
    public Guid Id { get; private set; }
    public string LanguageCode { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;
    public string? Module { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private LocalizationResource() { }

    /// <summary>Creates a new localization resource entry.</summary>
    public static LocalizationResource Create(
        string languageCode, string key, string value, string? module = null)
    {
        return new LocalizationResource
        {
            Id = Guid.NewGuid(),
            LanguageCode = languageCode.Trim().ToLowerInvariant(),
            Key = key,
            Value = value,
            Module = module?.Trim().ToLowerInvariant(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Updates the translation value.</summary>
    public void UpdateValue(string value)
    {
        Value = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
