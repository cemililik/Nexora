using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a role with assignable permissions within a tenant.</summary>
public sealed class Role : AuditableEntity<RoleId>, IAggregateRoot
{
    /// <summary>Gets the tenant this role belongs to.</summary>
    public TenantId TenantId { get; private set; }

    /// <summary>Gets the display name of the role.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the optional description of the role.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets a value indicating whether this is a system-defined role.</summary>
    public bool IsSystemRole { get; private set; }

    /// <summary>Gets a value indicating whether this role is active.</summary>
    public bool IsActive { get; private set; } = true;

    private readonly List<RolePermission> _permissions = [];

    /// <summary>Gets the permissions assigned to this role.</summary>
    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

    /// <summary>Creates a new role for a tenant.</summary>
    public static Role Create(TenantId tenantId, string name, string? description = null, bool isSystem = false)
    {
        return new Role
        {
            Id = RoleId.New(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            IsSystemRole = isSystem
        };
    }

    /// <summary>Assigns a permission to this role if not already assigned.</summary>
    public void AssignPermission(Permission permission)
    {
        if (_permissions.Any(rp => rp.PermissionId == permission.Id))
            return;

        _permissions.Add(RolePermission.Create(Id, permission.Id));
        AddDomainEvent(new RolePermissionChangedEvent(Id, permission.Id, PermissionAction.Assigned));
    }

    /// <summary>Revokes a permission from this role.</summary>
    public void RevokePermission(PermissionId permissionId)
    {
        var rp = _permissions.FirstOrDefault(p => p.PermissionId == permissionId);
        if (rp is not null)
        {
            _permissions.Remove(rp);
            AddDomainEvent(new RolePermissionChangedEvent(Id, permissionId, PermissionAction.Revoked));
        }
    }
}
