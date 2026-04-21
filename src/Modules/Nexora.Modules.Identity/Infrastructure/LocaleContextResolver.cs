using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Localization;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure;

/// <summary>
/// Resolves the effective <see cref="ILocaleContext"/> for the current request.
/// Resolution order (3-tier): User preference → Organization settings → Tenant default → Platform default.
/// <list type="bullet">
///   <item>Language: User.PreferredLanguage → Org.DefaultLanguage → Tenant locale language part → "en"</item>
///   <item>Locale: Org.DefaultLocale → Tenant.DefaultLocale → "en-US"</item>
///   <item>Currency: Org.DefaultCurrency → Tenant.DefaultCurrency → "USD"</item>
///   <item>Timezone: Org.Timezone → Tenant.DefaultTimezone → "UTC"</item>
///   <item>DocumentLanguage: Org.DefaultLanguage → Tenant.DefaultDocumentLanguage → "en"</item>
/// </list>
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
        // Read settings synchronously — resolver is called in property accessors.
        // All DbContexts use the connection pool and are fast for single-row reads.
        // Background jobs set tenant context before accessing ILocaleContext, so this is safe.
        TenantSettings tenantSettings;
        string? userPreferredLanguage = null;
        string? orgTimezone = null;
        string? orgCurrency = null;
        string? orgLanguage = null;
        string? orgLocale = null;

        ITenantContext? contextOrNull;
        try
        {
            contextOrNull = tenantContextAccessor.Current;
        }
        catch (InvalidOperationException)
        {
            // No tenant context (anonymous endpoint, system job without tenant) — platform defaults.
            return PlatformDefaults();
        }

        var context = contextOrNull;
        if (string.IsNullOrEmpty(context.TenantId) || !Guid.TryParse(context.TenantId, out _))
            return PlatformDefaults();

        var tenantId = TenantId.Parse(context.TenantId);

        // Tier 3: Tenant settings from public schema
        var settingsJson = platformDbContext.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Settings)
            .FirstOrDefault();

        tenantSettings = TenantSettings.FromJson(settingsJson);

        // Tier 2: Organization settings — MUST filter by TenantId to prevent cross-tenant data leak.
        if (!string.IsNullOrEmpty(context.OrganizationId) &&
            Guid.TryParse(context.OrganizationId, out var orgGuid))
        {
            var orgId = OrganizationId.From(orgGuid);
            var orgData = identityDbContext.Organizations
                .Where(o => o.Id == orgId && o.TenantId == tenantId)
                .Select(o => new { o.Timezone, o.DefaultCurrency, o.DefaultLanguage, o.DefaultLocale })
                .FirstOrDefault();

            if (orgData is not null)
            {
                orgTimezone = orgData.Timezone;
                orgCurrency = orgData.DefaultCurrency;
                orgLanguage = orgData.DefaultLanguage;
                orgLocale = orgData.DefaultLocale;
            }
        }

        // Tier 1: User language preference — MUST scope by TenantId.
        if (!string.IsNullOrEmpty(context.UserId))
        {
            userPreferredLanguage = identityDbContext.Users
                .Where(u => u.KeycloakUserId == context.UserId && u.TenantId == tenantId)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefault();
        }

        // Derive language from locale if user has no preference
        var tenantLanguage = tenantSettings.DefaultLocale.Split('-')[0].ToLowerInvariant();

        return new ResolvedLocale(
            Language: userPreferredLanguage ?? orgLanguage ?? tenantLanguage,
            Locale: orgLocale ?? tenantSettings.DefaultLocale,
            Currency: orgCurrency ?? tenantSettings.DefaultCurrency,
            Timezone: orgTimezone ?? tenantSettings.DefaultTimezone,
            DocumentLanguage: orgLanguage ?? tenantSettings.DefaultDocumentLanguage
        );
    }

    private static ResolvedLocale PlatformDefaults()
    {
        var defaults = TenantSettings.Default;
        var language = defaults.DefaultLocale.Split('-')[0].ToLowerInvariant();
        return new ResolvedLocale(
            Language: language,
            Locale: defaults.DefaultLocale,
            Currency: defaults.DefaultCurrency,
            Timezone: defaults.DefaultTimezone,
            DocumentLanguage: defaults.DefaultDocumentLanguage);
    }

    private sealed record ResolvedLocale(
        string Language,
        string Locale,
        string Currency,
        string Timezone,
        string DocumentLanguage);
}
