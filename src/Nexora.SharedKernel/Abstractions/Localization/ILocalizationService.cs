namespace Nexora.SharedKernel.Abstractions.Localization;

/// <summary>
/// Resolves localization keys to translated strings.
/// Supports tenant-specific overrides on top of shared base translations.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Resolves a single localization key. Returns null if not found.</summary>
    Task<string?> GetAsync(string key, string languageCode, Guid? tenantId = null, CancellationToken ct = default);

    /// <summary>Resolves multiple localization keys. Missing keys are omitted from the result.</summary>
    Task<Dictionary<string, string>> GetManyAsync(
        IEnumerable<string> keys, string languageCode, Guid? tenantId = null, CancellationToken ct = default);

    /// <summary>Returns all translations for a module in the given language.</summary>
    Task<Dictionary<string, string>> GetByModuleAsync(
        string module, string languageCode, Guid? tenantId = null, CancellationToken ct = default);

    /// <summary>Returns all translations for the given language.</summary>
    Task<Dictionary<string, string>> GetAllAsync(
        string languageCode, Guid? tenantId = null, CancellationToken ct = default);
}
