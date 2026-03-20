using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Abstractions.Configuration;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Tenant-specific configuration stored in the tenant's schema.
/// </summary>
public sealed class DatabaseTenantConfiguration(
    TenantConfigDbContext dbContext) : ITenantConfiguration
{
    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var entry = await dbContext.Configurations
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
