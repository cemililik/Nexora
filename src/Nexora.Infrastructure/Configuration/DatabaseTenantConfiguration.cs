using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.Configuration;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Backward-compatible shim over <see cref="IConfigurationResolver"/>. Existing callers
/// keep the old <c>Get/Set</c> semantics against <c>platform_tenant_config</c> (no org
/// layer, no cap check). New code MUST depend on <see cref="IConfigurationResolver"/>
/// directly — this type is preserved only to avoid a sweeping signature change during
/// the Phase 1.5.6 rollout (ADR-0025).
/// </summary>
/// <remarks>
/// The resolver is NOT consulted here: reads go straight to the tenant layer so that
/// legacy keys which are not yet cap-managed keep behaving exactly as they did before.
/// When a key graduates to org-scope management, its call site should migrate to
/// <see cref="IConfigurationResolver"/> in the same PR.
/// </remarks>
public sealed class DatabaseTenantConfiguration(
    TenantConfigDbContext dbContext) : ITenantConfiguration
{
    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var entry = await dbContext.Configurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, ct);

        if (entry is null)
            return default!;

        return JsonSerializer.Deserialize<T>(entry.Value)!;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        var entry = await dbContext.Configurations
            .FirstOrDefaultAsync(c => c.Key == key, ct);

        if (entry is null)
        {
            dbContext.Configurations.Add(new TenantConfigEntry
            {
                Key = key,
                Value = json,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            entry.Value = json;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
