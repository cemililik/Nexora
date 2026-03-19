using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.MultiTenancy;

/// <summary>
/// Holds the current tenant context. Set per-request by middleware.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = default!;
    public string SchemaName { get; set; } = default!;
    public string? OrganizationId { get; set; }
    public string? UserId { get; set; }
}

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContext?> _current = new();

    public ITenantContext Current =>
        _current.Value ?? throw new InvalidOperationException("Tenant context not set.");

    public void SetTenant(string tenantId, string? organizationId = null, string? userId = null)
    {
        _current.Value = new TenantContext
        {
            TenantId = tenantId,
            SchemaName = $"tenant_{tenantId}",
            OrganizationId = organizationId,
            UserId = userId
        };
    }
}
