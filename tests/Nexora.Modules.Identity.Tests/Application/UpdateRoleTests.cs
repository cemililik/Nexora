using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateRoleTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public UpdateRoleTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task UpdateRole_WithValidData_UpdatesNameAndDescription()
    {
        var role = Role.Create(_tenantId, "OldName", "Old description");
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(role.Id.Value, "NewName", "New description", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("NewName");
        result.Value.Description.Should().Be("New description");
        result.Value.IsSystemRole.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRole_WithSystemRole_ReturnsFailure()
    {
        var role = Role.Create(_tenantId, "Admin", null, isSystem: true);
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(role.Id.Value, "RenamedAdmin", "desc", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_system_role_immutable");
    }

    [Fact]
    public async Task UpdateRole_WithDuplicateName_ReturnsFailure()
    {
        var roleA = Role.Create(_tenantId, "RoleA", "First role");
        var roleB = Role.Create(_tenantId, "RoleB", "Second role");
        await _dbContext.Roles.AddRangeAsync(roleA, roleB);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(roleB.Id.Value, "RoleA", "Trying duplicate", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_name_taken");
    }

    [Fact]
    public async Task UpdateRole_WithNonExistentRole_ReturnsFailure()
    {
        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(Guid.NewGuid(), "Ghost", null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_not_found");
    }

    [Fact]
    public async Task UpdateRole_WithSameNameAsSelf_Succeeds()
    {
        var role = Role.Create(_tenantId, "Unchanged", "Keep this name");
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(role.Id.Value, "Unchanged", "Updated description only", null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Unchanged");
        result.Value.Description.Should().Be("Updated description only");
    }

    [Fact]
    public async Task UpdateRole_WithPermissionChanges_ReconcilePermissions()
    {
        var permA = Permission.Create("crm", "contacts", "read");
        var permB = Permission.Create("crm", "contacts", "write");
        await _dbContext.Permissions.AddRangeAsync(permA, permB);
        await _dbContext.SaveChangesAsync();

        var role = Role.Create(_tenantId, "TestRole", "desc");
        role.AssignPermission(permA);
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateRoleHandler(_dbContext, _tenantAccessor, NullLogger<UpdateRoleHandler>.Instance);
        var command = new UpdateRoleCommand(role.Id.Value, "TestRole", "desc", [permB.Id.Value]);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.Should().NotContain("crm.contacts.read");
        result.Value.Permissions.Should().Contain("crm.contacts.write");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
