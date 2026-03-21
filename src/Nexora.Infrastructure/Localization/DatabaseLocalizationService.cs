using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Localization;

namespace Nexora.Infrastructure.Localization;

/// <summary>
/// Resolves localization keys from the database with two-level caching.
/// Base translations come from <c>localization_resources</c>; tenant-specific
/// overrides from <c>localization_overrides</c> take precedence when present.
/// </summary>
public sealed class DatabaseLocalizationService(
    LocalizationDbContext dbContext,
    ICacheService cacheService,
    ILogger<DatabaseLocalizationService> logger) : ILocalizationService
{
    private static readonly CacheOptions TranslationCacheOptions = new()
    {
        L1Ttl = TimeSpan.FromMinutes(5),
        L2Ttl = TimeSpan.FromMinutes(30)
    };

    /// <inheritdoc />
    public async Task<string?> GetAsync(
        string key, string languageCode, Guid? tenantId = null, CancellationToken ct = default)
    {
        var lang = Normalize(languageCode);

        // Check tenant override first
        if (tenantId.HasValue)
        {
            var overrideValue = await dbContext.Overrides
                .Where(o => o.TenantId == tenantId.Value && o.LanguageCode == lang && o.Key == key)
                .Select(o => o.Value)
                .FirstOrDefaultAsync(ct);

            if (overrideValue is not null)
                return overrideValue;
        }

        // Fall back to base resource
        var baseValue = await dbContext.Resources
            .Where(r => r.LanguageCode == lang && r.Key == key)
            .Select(r => r.Value)
            .FirstOrDefaultAsync(ct);

        if (baseValue is null)
            logger.LogDebug("Localization key {Key} not found for language {LanguageCode}", key, lang);

        return baseValue;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetManyAsync(
        IEnumerable<string> keys, string languageCode, Guid? tenantId = null, CancellationToken ct = default)
    {
        var lang = Normalize(languageCode);
        var keyList = keys.ToList();

        if (keyList.Count == 0)
            return new Dictionary<string, string>();

        // Load base resources
        var result = await dbContext.Resources
            .Where(r => r.LanguageCode == lang && keyList.Contains(r.Key))
            .ToDictionaryAsync(r => r.Key, r => r.Value, ct);

        // Apply tenant overrides
        if (tenantId.HasValue)
        {
            var overrides = await dbContext.Overrides
                .Where(o => o.TenantId == tenantId.Value && o.LanguageCode == lang && keyList.Contains(o.Key))
                .ToDictionaryAsync(o => o.Key, o => o.Value, ct);

            foreach (var kvp in overrides)
                result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetByModuleAsync(
        string module, string languageCode, Guid? tenantId = null, CancellationToken ct = default)
    {
        var lang = Normalize(languageCode);
        var mod = module.Trim().ToLowerInvariant();
        var cacheKey = tenantId.HasValue
            ? $"localization:{tenantId.Value}:{lang}:{mod}"
            : $"localization:{lang}:{mod}";

        return await cacheService.GetOrSetAsync(cacheKey, async _ =>
        {
            var result = await dbContext.Resources
                .Where(r => r.LanguageCode == lang && r.Module == mod)
                .ToDictionaryAsync(r => r.Key, r => r.Value, ct);

            if (tenantId.HasValue)
            {
                // Load overrides matching the module prefix pattern: lockey_{module}_*
                var prefix = $"lockey_{mod}_";
                var overrides = await dbContext.Overrides
                    .Where(o => o.TenantId == tenantId.Value && o.LanguageCode == lang && o.Key.StartsWith(prefix))
                    .ToDictionaryAsync(o => o.Key, o => o.Value, ct);

                foreach (var kvp in overrides)
                    result[kvp.Key] = kvp.Value;
            }

            return result;
        }, TranslationCacheOptions, ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetAllAsync(
        string languageCode, Guid? tenantId = null, CancellationToken ct = default)
    {
        var lang = Normalize(languageCode);
        var cacheKey = tenantId.HasValue
            ? $"localization:{tenantId.Value}:{lang}:all"
            : $"localization:{lang}:all";

        return await cacheService.GetOrSetAsync(cacheKey, async _ =>
        {
            var result = await dbContext.Resources
                .Where(r => r.LanguageCode == lang)
                .ToDictionaryAsync(r => r.Key, r => r.Value, ct);

            if (tenantId.HasValue)
            {
                var overrides = await dbContext.Overrides
                    .Where(o => o.TenantId == tenantId.Value && o.LanguageCode == lang)
                    .ToDictionaryAsync(o => o.Key, o => o.Value, ct);

                foreach (var kvp in overrides)
                    result[kvp.Key] = kvp.Value;
            }

            return result;
        }, TranslationCacheOptions, ct);
    }

    private static string Normalize(string languageCode) =>
        languageCode.Trim().ToLowerInvariant();
}
