namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>
/// Provides the current tenant context resolved from the JWT.
/// </summary>
public interface ITenantContext
{
    /// <summary>The unique tenant identifier.</summary>
    string TenantId { get; }

    /// <summary>The PostgreSQL schema name for the tenant.</summary>
    string SchemaName { get; }

    /// <summary>The current organization within the tenant, if applicable.</summary>
    string? OrganizationId { get; }

    /// <summary>The authenticated user ID, if available.</summary>
    string? UserId { get; }
}

/// <summary>
/// Allows setting tenant context (used by middleware and background jobs).
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>Gets the current tenant context. Throws if not set.</summary>
    ITenantContext Current { get; }

    /// <summary>Sets the tenant context for the current async flow.</summary>
    void SetTenant(string tenantId, string? organizationId = null, string? userId = null);
}
