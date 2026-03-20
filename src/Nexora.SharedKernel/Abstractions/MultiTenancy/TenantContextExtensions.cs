namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>Extension methods for safe tenant context value parsing.</summary>
public static class TenantContextExtensions
{
    /// <summary>Safely parses TenantId as Guid. Returns null if invalid.</summary>
    public static Guid? TryGetTenantGuid(this ITenantContext context)
    {
        return Guid.TryParse(context.TenantId, out var id) ? id : null;
    }

    /// <summary>Safely parses OrganizationId as Guid. Returns null if missing or invalid.</summary>
    public static Guid? TryGetOrganizationGuid(this ITenantContext context)
    {
        return Guid.TryParse(context.OrganizationId, out var id) ? id : null;
    }
}
