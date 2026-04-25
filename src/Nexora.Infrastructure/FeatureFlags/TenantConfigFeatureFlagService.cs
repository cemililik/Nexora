using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Configuration;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.FeatureFlags;

namespace Nexora.Infrastructure.FeatureFlags;

/// <summary>
/// T-016: default <see cref="IFeatureFlagService"/> implementation backed
/// by <see cref="ITenantConfiguration"/>. Flag values live under the
/// <c>feature_flags.{key}</c> namespace so they share storage,
/// invalidation, and audit with the rest of tenant config.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a separate <see cref="TenantConfigDbContext"/> read for
/// <see cref="GetAllAsync"/>?</b> <see cref="ITenantConfiguration"/> exposes
/// per-key reads only — listing every flag would require N round-trips.
/// The admin UI calls <see cref="GetAllAsync"/>, which queries the
/// <c>platform_tenant_config</c> table directly with a single
/// <c>WHERE Key LIKE 'feature_flags.%'</c> filter.
/// </para>
/// <para>
/// <b>User-id dimension.</b> The default backend ignores
/// <paramref name="userId"/> in <see cref="IsEnabledAsync"/> — flags here
/// are tenant-scoped booleans. A future LaunchDarkly backend would hash
/// on the user id for percentage rollouts; the call sites stay unchanged.
/// </para>
/// </remarks>
public sealed class TenantConfigFeatureFlagService(
    ITenantConfiguration tenantConfig,
    TenantConfigDbContext configDb) : IFeatureFlagService
{
    /// <summary>Storage prefix for all flag keys.</summary>
    public const string FlagKeyPrefix = "feature_flags.";

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string flagKey, Guid? userId = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        // userId is intentionally ignored — see <remarks>.
        var storageKey = FlagKeyPrefix + flagKey;
        return await tenantConfig.GetAsync<bool>(storageKey, ct);
    }

    /// <inheritdoc />
    public Task SetAsync(string flagKey, bool enabled, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        var storageKey = FlagKeyPrefix + flagKey;
        return tenantConfig.SetAsync(storageKey, enabled, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken ct = default)
    {
        // Query the tenant-config table directly — ITenantConfiguration is
        // single-key by design and we want one round-trip for the admin
        // listing. Filter on the prefix; trim it from the returned key so
        // callers see the bare flag identifier.
        var rows = await configDb.Configurations
            .AsNoTracking()
            .Where(c => c.Key.StartsWith(FlagKeyPrefix))
            .ToListAsync(ct);

        var result = new Dictionary<string, bool>(rows.Count, StringComparer.Ordinal);
        foreach (var row in rows)
        {
            // Stored as JSONB — true / false / "true" / "false" all need to deserialise.
            // Trim the prefix off the storage key for the public surface.
            var bareKey = row.Key.AsSpan(FlagKeyPrefix.Length).ToString();
            if (bool.TryParse(row.Value?.Trim('"'), out var enabled))
                result[bareKey] = enabled;
        }
        return result;
    }
}
