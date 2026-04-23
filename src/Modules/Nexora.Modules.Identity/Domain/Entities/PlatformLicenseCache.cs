using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>
/// Caches NMP (Nexora Management Portal) license verification results per tenant/module.
/// Used by the NMP license verifier (Phase NMP.1) to avoid repeated NMP API calls.
/// In Phase 1.5.2 this table is created but not yet populated — <c>NullLicenseVerifier</c> always returns true.
/// </summary>
public sealed class PlatformLicenseCache
{
    /// <summary>Tenant the license entry belongs to.</summary>
    public TenantId TenantId { get; private set; } = default!;

    /// <summary>Module name (e.g. "contacts", "crm").</summary>
    public string ModuleName { get; private set; } = default!;

    /// <summary>Whether the tenant is licensed to use this module.</summary>
    public bool IsLicensed { get; private set; }

    /// <summary>When this cache entry was last refreshed from NMP.</summary>
    public DateTimeOffset CachedAt { get; private set; }

    /// <summary>When this cache entry expires and must be re-verified with NMP.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    private PlatformLicenseCache() { }

    /// <summary>Creates a new cache entry from an NMP verification response.</summary>
    public static PlatformLicenseCache Create(
        TenantId tenantId,
        string moduleName,
        bool isLicensed,
        DateTimeOffset expiresAt)
    {
        return new PlatformLicenseCache
        {
            TenantId = tenantId,
            ModuleName = moduleName,
            IsLicensed = isLicensed,
            CachedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>Updates the cached result with a fresh NMP response.</summary>
    public void Refresh(bool isLicensed, DateTimeOffset expiresAt)
    {
        IsLicensed = isLicensed;
        CachedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
    }

    /// <summary>Returns <see langword="true"/> when the cache entry has passed its expiry time.</summary>
    public bool IsExpired(DateTimeOffset? now = null) =>
        (now ?? DateTimeOffset.UtcNow) >= ExpiresAt;
}
