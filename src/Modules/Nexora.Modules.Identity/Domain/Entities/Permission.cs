using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>
/// Permission: {module}.{resource}.{action} format.
/// </summary>
public sealed class Permission : Entity<PermissionId>
{
    public string Module { get; private set; } = default!;
    public string Resource { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? Description { get; private set; }

    /// <summary>
    /// Determines who may hold this permission.
    /// <see cref="PermissionScope.Platform"/> permissions are reserved for Nexora platform
    /// operators and can never be assigned to tenant-scoped roles.
    /// </summary>
    public PermissionScope Scope { get; private set; } = PermissionScope.Tenant;

    /// <summary>Full permission key: module.resource.action</summary>
    public string Key => $"{Module}.{Resource}.{Action}";

    /// <summary>Updates the scope classification. Used by idempotent seed migrations.</summary>
    internal void SetScope(PermissionScope scope) => Scope = scope;

    private Permission() { }

    /// <summary>Creates a new permission entry.</summary>
    public static Permission Create(
        string module,
        string resource,
        string action,
        string? description = null,
        PermissionScope scope = PermissionScope.Tenant)
    {
        return new Permission
        {
            Id = PermissionId.New(),
            Module = module,
            Resource = resource,
            Action = action,
            Description = description,
            Scope = scope
        };
    }
}
