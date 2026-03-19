namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Tenant-specific configuration stored in database.
/// Falls back to platform defaults if no tenant override exists.
/// </summary>
public interface ITenantConfiguration
{
    Task<T> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
}
