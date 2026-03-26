using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class AssignUserRolesTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public AssignUserRolesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task AssignUserRoles_WithValidData_AssignsRolesAndReturnsSuccess()
    {
        var user = User.Create(_tenantId, "kc-1", "a@t.com", "A", "B");
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(user.Id, orgId);
        var roleA = Role.Create(_tenantId, "Admin");
        var roleB = Role.Create(_tenantId, "Editor");

        _dbContext.Users.Add(user);
        _dbContext.OrganizationUsers.Add(orgUser);
        _dbContext.Roles.AddRange(roleA, roleB);
        await _dbContext.SaveChangesAsync();

        var handler = new AssignUserRolesHandler(
            _dbContext, _tenantAccessor, NullLogger<AssignUserRolesHandler>.Instance);

        var result = await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value,
                [roleA.Id.Value, roleB.Id.Value]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedOrgUser = await _dbContext.OrganizationUsers
            .Include(ou => ou.UserRoles)
            .FirstAsync(ou => ou.Id == orgUser.Id);
        updatedOrgUser.UserRoles.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssignUserRoles_WithUserNotInOrganization_ReturnsFailure()
    {
        var handler = new AssignUserRolesHandler(
            _dbContext, _tenantAccessor, NullLogger<AssignUserRolesHandler>.Instance);

        var result = await handler.Handle(
            new AssignUserRolesCommand(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_in_org");
    }

    [Fact]
    public async Task AssignUserRoles_WithInvalidRoleIds_ReturnsFailure()
    {
        var user = User.Create(_tenantId, "kc-2", "b@t.com", "B", "C");
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(user.Id, orgId);

        _dbContext.Users.Add(user);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new AssignUserRolesHandler(
            _dbContext, _tenantAccessor, NullLogger<AssignUserRolesHandler>.Instance);

        var result = await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value, [Guid.NewGuid()]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_invalid_roles");
    }

    [Fact]
    public async Task AssignUserRoles_WithRoleReconciliation_RemovesOldAndAddsNew()
    {
        var user = User.Create(_tenantId, "kc-3", "c@t.com", "C", "D");
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(user.Id, orgId);
        var roleA = Role.Create(_tenantId, "RoleA");
        var roleB = Role.Create(_tenantId, "RoleB");

        _dbContext.Users.Add(user);
        _dbContext.OrganizationUsers.Add(orgUser);
        _dbContext.Roles.AddRange(roleA, roleB);
        await _dbContext.SaveChangesAsync();

        // First assign roleA
        var handler = new AssignUserRolesHandler(
            _dbContext, _tenantAccessor, NullLogger<AssignUserRolesHandler>.Instance);

        await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value, [roleA.Id.Value]),
            CancellationToken.None);

        // Now reconcile: replace roleA with roleB
        var result = await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value, [roleB.Id.Value]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedOrgUser = await _dbContext.OrganizationUsers
            .Include(ou => ou.UserRoles)
            .FirstAsync(ou => ou.Id == orgUser.Id);
        updatedOrgUser.UserRoles.Should().HaveCount(1);
        updatedOrgUser.UserRoles.Single().RoleId.Should().Be(roleB.Id);
    }

    [Fact]
    public async Task AssignUserRoles_WithEmptyRoleList_RemovesAllRoles()
    {
        var user = User.Create(_tenantId, "kc-4", "d@t.com", "D", "E");
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(user.Id, orgId);
        var role = Role.Create(_tenantId, "ToRemove");

        _dbContext.Users.Add(user);
        _dbContext.OrganizationUsers.Add(orgUser);
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        var handler = new AssignUserRolesHandler(
            _dbContext, _tenantAccessor, NullLogger<AssignUserRolesHandler>.Instance);

        // First assign a role
        await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value, [role.Id.Value]),
            CancellationToken.None);

        // Now assign empty list
        var result = await handler.Handle(
            new AssignUserRolesCommand(user.Id.Value, orgId.Value, []),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var updatedOrgUser = await _dbContext.OrganizationUsers
            .Include(ou => ou.UserRoles)
            .FirstAsync(ou => ou.Id == orgUser.Id);
        updatedOrgUser.UserRoles.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
