namespace Nexora.SharedKernel.Abstractions.MultiTenancy;

/// <summary>Extension methods for safe tenant context value parsing.</summary>
public static class TenantContextExtensions
{
    /// <summary>Safely parses TenantId as Guid. Returns null if invalid.</summary>
    public static Guid? TryGetTenantGuid(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Guid.TryParse(context.TenantId, out var id) ? id : null;
    }

    /// <summary>Safely parses OrganizationId as Guid. Returns null if missing or invalid.</summary>
    public static Guid? TryGetOrganizationGuid(this ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Guid.TryParse(context.OrganizationId, out var id) ? id : null;
    }

    /// <summary>
    /// Safely retrieves the current tenant context without throwing.
    /// Returns null if tenant context is not available (e.g., outside request scope).
    /// </summary>
    public static ITenantContext? TryGetCurrent(this ITenantContextAccessor accessor)
    {
        if (accessor is null) return null;

        try
        {
            return accessor.Current;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
