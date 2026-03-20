using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a permission assigned to a role.</summary>
public sealed class RolePermission : Entity<RolePermissionId>
{
    public RoleId RoleId { get; private set; }
    public PermissionId PermissionId { get; private set; }

    private RolePermission() { }

    /// <summary>Creates a new role-permission association.</summary>
    public static RolePermission Create(RoleId roleId, PermissionId permissionId)
    {
        return new RolePermission
        {
            Id = RolePermissionId.New(),
            RoleId = roleId,
            PermissionId = permissionId
        };
    }
}
