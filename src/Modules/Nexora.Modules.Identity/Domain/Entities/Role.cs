using Nexora.Modules.Identity.Domain.Events;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class Role : AuditableEntity<RoleId>, IAggregateRoot
{
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public bool IsActive { get; private set; } = true;

    private readonly List<RolePermission> _permissions = [];
    public IReadOnlyList<RolePermission> Permissions => _permissions.AsReadOnly();

    private Role() { }

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

    public void AssignPermission(Permission permission)
    {
        if (_permissions.Any(rp => rp.PermissionId == permission.Id))
            return;

        _permissions.Add(RolePermission.Create(Id, permission.Id));
        AddDomainEvent(new RolePermissionChangedEvent(Id, "assigned"));
    }

    public void RevokePermission(PermissionId permissionId)
    {
        var rp = _permissions.FirstOrDefault(p => p.PermissionId == permissionId);
        if (rp is not null)
        {
            _permissions.Remove(rp);
            AddDomainEvent(new RolePermissionChangedEvent(Id, "revoked"));
        }
    }
}
