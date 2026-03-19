using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;

namespace Nexora.Modules.Identity.Tests.Domain;

public sealed class RoleTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var role = Role.Create(_tenantId, "Admin", "Full access", isSystem: true);

        role.Id.Value.Should().NotBeEmpty();
        role.TenantId.Should().Be(_tenantId);
        role.Name.Should().Be("Admin");
        role.Description.Should().Be("Full access");
        role.IsSystemRole.Should().BeTrue();
        role.IsActive.Should().BeTrue();
        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void AssignPermission_ShouldAddToCollection()
    {
        var role = Role.Create(_tenantId, "Editor");
        var permission = Permission.Create("crm", "contacts", "read");

        role.AssignPermission(permission);

        role.Permissions.Should().ContainSingle();
        role.Permissions[0].PermissionId.Should().Be(permission.Id);
    }

    [Fact]
    public void AssignPermission_ShouldRaiseDomainEvent()
    {
        var role = Role.Create(_tenantId, "Editor");
        var permission = Permission.Create("crm", "contacts", "read");

        role.AssignPermission(permission);

        role.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<Nexora.Modules.Identity.Domain.Events.RolePermissionChangedEvent>();
    }

    [Fact]
    public void AssignPermission_Duplicate_ShouldNotAddTwice()
    {
        var role = Role.Create(_tenantId, "Editor");
        var permission = Permission.Create("crm", "contacts", "read");

        role.AssignPermission(permission);
        role.AssignPermission(permission);

        role.Permissions.Should().ContainSingle();
    }

    [Fact]
    public void RevokePermission_ShouldRemoveFromCollection()
    {
        var role = Role.Create(_tenantId, "Editor");
        var permission = Permission.Create("crm", "contacts", "read");
        role.AssignPermission(permission);

        role.RevokePermission(permission.Id);

        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void RevokePermission_NonExistent_ShouldDoNothing()
    {
        var role = Role.Create(_tenantId, "Editor");

        role.RevokePermission(PermissionId.New());

        role.Permissions.Should().BeEmpty();
    }
}

public sealed class PermissionTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        var permission = Permission.Create("identity", "users", "create", "Create users");

        permission.Id.Value.Should().NotBeEmpty();
        permission.Module.Should().Be("identity");
        permission.Resource.Should().Be("users");
        permission.Action.Should().Be("create");
        permission.Description.Should().Be("Create users");
    }

    [Fact]
    public void Key_ShouldReturnFormattedKey()
    {
        var permission = Permission.Create("crm", "contacts", "read");

        permission.Key.Should().Be("crm.contacts.read");
    }
}
