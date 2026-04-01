using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Resolves the effective <see cref="ILocaleContext"/> for the current request.
/// Resolution order: User preference → Tenant default → Platform default.
/// Result is lazy-initialized and cached within the scoped DI lifetime (one resolution per request).
/// </summary>
public sealed class LocaleContextResolver(
    IdentityDbContext identityDbContext,
    PlatformDbContext platformDbContext,
    ITenantContextAccessor tenantContextAccessor) : ILocaleContext
{
    private ResolvedLocale? _resolved;

    private ResolvedLocale Resolved => _resolved ??= Resolve();

    /// <inheritdoc />
    public string Language => Resolved.Language;

    /// <inheritdoc />
    public string Locale => Resolved.Locale;

    /// <inheritdoc />
    public string Currency => Resolved.Currency;

    /// <inheritdoc />
    public string Timezone => Resolved.Timezone;

    /// <inheritdoc />
    public string DocumentLanguage => Resolved.DocumentLanguage;

    private ResolvedLocale Resolve()
    {
        // Read tenant settings synchronously — resolver is called in property accessors.
        // Both DbContexts use the connection pool and are fast for single-row reads.
        // Background jobs set tenant context before accessing ILocaleContext, so this is safe.
        TenantSettings tenantSettings;
        string? userPreferredLanguage = null;

        try
        {
            var context = tenantContextAccessor.Current;

            // Tenant settings from public schema
            var settingsJson = platformDbContext.Tenants
                .Where(t => t.Id == TenantId.Parse(context.TenantId))
                .Select(t => t.Settings)
                .FirstOrDefault();

            tenantSettings = TenantSettings.FromJson(settingsJson);

            // User language preference from tenant schema
            if (!string.IsNullOrEmpty(context.UserId))
            {
                userPreferredLanguage = identityDbContext.Users
                    .Where(u => u.KeycloakUserId == context.UserId)
                    .Select(u => u.PreferredLanguage)
                    .FirstOrDefault();
            }
        }
        catch
        {
            // Tenant context not set (e.g. anonymous endpoint, system job without tenant).
            // Fall back to platform defaults.
            tenantSettings = TenantSettings.Default;
        }

        // Derive language from locale if user has no preference
        var tenantLanguage = tenantSettings.DefaultLocale.Split('-')[0].ToLowerInvariant();

        return new ResolvedLocale(
            Language: userPreferredLanguage ?? tenantLanguage,
            Locale: tenantSettings.DefaultLocale,
            Currency: tenantSettings.DefaultCurrency,
            Timezone: tenantSettings.DefaultTimezone,
            DocumentLanguage: tenantSettings.DefaultDocumentLanguage
        );
    }

    private sealed record ResolvedLocale(
        string Language,
        string Locale,
        string Currency,
        string Timezone,
        string DocumentLanguage);
}
