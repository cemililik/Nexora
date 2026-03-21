namespace Nexora.Infrastructure.Localization.Entities;

/// <summary>
/// Tenant-specific translation override. When present, takes precedence over
/// the shared <see cref="LocalizationResource"/> for the same key and language.
/// </summary>
public sealed class LocalizationOverride
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string LanguageCode { get; private set; } = null!;
    public string Key { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    private LocalizationOverride() { }

    /// <summary>Creates a new tenant-specific translation override.</summary>
    public static LocalizationOverride Create(
        Guid tenantId, string languageCode, string key, string value)
    {
        return new LocalizationOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LanguageCode = languageCode.Trim().ToLowerInvariant(),
            Key = key,
            Value = value
        };
    }

    /// <summary>Updates the override value.</summary>
    public void UpdateValue(string value)
    {
        Value = value;
    }
}
