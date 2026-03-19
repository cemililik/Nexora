namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>
/// Provides the current tenant context resolved from the JWT.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    string SchemaName { get; }
    string? OrganizationId { get; }
    string? UserId { get; }
}

/// <summary>
/// Allows setting tenant context (used by middleware and background jobs).
/// </summary>
public interface ITenantContextAccessor
{
    ITenantContext Current { get; }
    void SetTenant(string tenantId, string? organizationId = null, string? userId = null);
}
