namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>Provides access to the list of active tenants for cross-tenant operations.</summary>
public interface IActiveTenantProvider
{
    /// <summary>Gets all active tenants.</summary>
    Task<IReadOnlyList<ActiveTenantInfo>> GetActiveTenantsAsync(CancellationToken ct = default);

    /// <summary>Gets active tenants that have a specific module installed.</summary>
    Task<IReadOnlyList<ActiveTenantInfo>> GetActiveTenantsWithModuleAsync(string moduleName, CancellationToken ct = default);
}

/// <summary>Information about an active tenant for job processing.</summary>
public sealed record ActiveTenantInfo(string TenantId, string SchemaName);
