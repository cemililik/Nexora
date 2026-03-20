namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Tenant-specific configuration stored in database.
/// Falls back to platform defaults if no tenant override exists.
/// </summary>
public interface ITenantConfiguration
{
    /// <summary>Gets a tenant configuration value by key, deserialized to the specified type.</summary>
    Task<T> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Sets a tenant configuration value, creating or updating the entry.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
}
