namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Tenant-scoped key/value configuration. Reads and writes resolve against the
/// current tenant context only — the organization id is intentionally NOT part
/// of the contract.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope contract (T-018):</b> every key managed through this interface is
/// tenant-global. Callers MUST have a tenant set on <c>ITenantContextAccessor</c>
/// before invoking either method; the organization id on the accessor is ignored.
/// A missing tenant throws — silent fallback to a "default" tenant is NOT a supported
/// mode.
/// </para>
/// <para>
/// Org-scoped values (per-organization overrides with compliance-cap precedence)
/// belong on <see cref="IConfigurationResolver"/>, not here. When a key graduates
/// to org scope it must migrate to the resolver in the same PR; <b>do not</b>
/// bolt an overload onto this interface.
/// </para>
/// <para>
/// <b>Seed-time usage (T-018 / ContactsModuleMigration):</b> module migrations
/// invoke <c>accessor.SetTenant(tenantId)</c> — the single-argument overload —
/// before calling <see cref="SetAsync{T}"/>. Passing a null organization id is
/// correct and expected; any implementation that rejects a null org from this
/// path is a bug.
/// </para>
/// </remarks>
public interface ITenantConfiguration
{
    /// <summary>
    /// Gets a tenant configuration value by key, deserialized to the specified type.
    /// Returns <c>default(T)</c> when the key is absent.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when no tenant context is set on the ambient <c>ITenantContextAccessor</c>.
    /// </exception>
    Task<T> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Sets a tenant configuration value, creating or updating the entry.
    /// Does NOT require an organization id; the write targets the tenant-global
    /// <c>platform_tenant_config</c> table.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when no tenant context is set on the ambient <c>ITenantContextAccessor</c>.
    /// </exception>
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
}
