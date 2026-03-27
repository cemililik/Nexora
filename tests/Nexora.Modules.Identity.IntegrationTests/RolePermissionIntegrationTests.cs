using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>Integration tests for role and permission management flows.</summary>
public sealed class RolePermissionIntegrationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public RolePermissionIntegrationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);
    }

    [Fact]
    public async Task CreateRole_AssignPermissions_ShouldPersist()
    {
        // Arrange: seed permissions
        var perm1 = Permission.Create("identity", "users", "read", "Read users");
        var perm2 = Permission.Create("identity", "users", "write", "Write users");
        await _dbContext.Permissions.AddRangeAsync(perm1, perm2);
        await _dbContext.SaveChangesAsync();

        // Act: create role with permissions
        var handler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        var result = await handler.Handle(
            new CreateRoleCommand("Admin", "Full admin role", [perm1.Id.Value, perm2.Id.Value]),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Admin");
        result.Value.Description.Should().Be("Full admin role");
        result.Value.Permissions.Should().HaveCount(2);
        result.Value.Permissions.Should().Contain("identity.users.read");
        result.Value.Permissions.Should().Contain("identity.users.write");

        // Verify in database
        var roleId = RoleId.From(result.Value.Id);
        var rolePermissions = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        rolePermissions.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateRole_ChangePermissions_ShouldReflect()
    {
        // Arrange: seed permissions
        var permRead = Permission.Create("crm", "contacts", "read");
        var permWrite = Permission.Create("crm", "contacts", "write");
        var permDelete = Permission.Create("crm", "contacts", "delete");
        await _dbContext.Permissions.AddRangeAsync(permRead, permWrite, permDelete);
        await _dbContext.SaveChangesAsync();

        // Create role with read+write
        var createHandler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateRoleCommand("Editor", "Edit stuff", [permRead.Id.Value, permWrite.Id.Value]),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var roleId = createResult.Value!.Id;

        // Act: update to read+delete (remove write, add delete)
        var updateHandler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var updateResult = await updateHandler.Handle(
            new UpdateRoleCommand(roleId, "Moderator", "Moderate stuff", [permRead.Id.Value, permDelete.Id.Value]),
            CancellationToken.None);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("Moderator");
        updateResult.Value.Description.Should().Be("Moderate stuff");
        updateResult.Value.Permissions.Should().HaveCount(2);
        updateResult.Value.Permissions.Should().Contain("crm.contacts.read");
        updateResult.Value.Permissions.Should().Contain("crm.contacts.delete");
        updateResult.Value.Permissions.Should().NotContain("crm.contacts.write");
    }

    [Fact]
    public async Task DeleteRole_ShouldRemovePermissionAssociations()
    {
        // Arrange: seed permission and create role
        var perm = Permission.Create("identity", "roles", "manage");
        await _dbContext.Permissions.AddAsync(perm);
        await _dbContext.SaveChangesAsync();

        var createHandler = new CreateRoleHandler(_dbContext, _tenantAccessor, NullLogger<CreateRoleHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateRoleCommand("Disposable Role", null, [perm.Id.Value]),
            CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var roleId = createResult.Value!.Id;

        // Verify role-permission exists
        var rpBefore = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == RoleId.From(roleId))
            .CountAsync();
        rpBefore.Should().Be(1);

        // Act: delete the role
        var deleteHandler = new DeleteRoleHandler(_dbContext, _tenantAccessor, NullLogger<DeleteRoleHandler>.Instance);
        var deleteResult = await deleteHandler.Handle(
            new DeleteRoleCommand(roleId), CancellationToken.None);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify: role no longer exists (soft-deleted, filtered by query filter)
        var roleAfter = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == RoleId.From(roleId));

        roleAfter.Should().BeNull();

        // Verify: role-permission associations are also removed
        var rpAfter = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == RoleId.From(roleId))
            .CountAsync();

        rpAfter.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
