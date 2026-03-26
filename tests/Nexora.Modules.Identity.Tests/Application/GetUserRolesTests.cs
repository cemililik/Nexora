using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetUserRolesTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetUserRolesTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task GetUserRoles_WithUserHavingRoles_ReturnsRolesWithPermissions()
    {
        var userId = UserId.New();
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(userId, orgId);
        _dbContext.OrganizationUsers.Add(orgUser);

        var perm1 = Permission.Create("crm", "contacts", "read");
        var perm2 = Permission.Create("crm", "contacts", "write");
        var perm3 = Permission.Create("crm", "deals", "read");
        _dbContext.Permissions.AddRange(perm1, perm2, perm3);

        var role1 = Role.Create(_tenantId, "Editor", "Editor role");
        role1.AssignPermission(perm1);
        role1.AssignPermission(perm2);
        _dbContext.Roles.Add(role1);

        var role2 = Role.Create(_tenantId, "Viewer", "Viewer role");
        role2.AssignPermission(perm3);
        _dbContext.Roles.Add(role2);

        _dbContext.UserRoles.Add(UserRole.Create(orgUser.Id, role1.Id));
        _dbContext.UserRoles.Add(UserRole.Create(orgUser.Id, role2.Id));
        await _dbContext.SaveChangesAsync();

        var handler = new GetUserRolesHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetUserRolesHandler>.Instance);
        var result = await handler.Handle(
            new GetUserRolesQuery(userId.Value, orgId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var editorRole = result.Value!.Single(r => r.Name == "Editor");
        editorRole.Permissions.Should().HaveCount(2);
        editorRole.Permissions.Should().Contain("crm.contacts.read");
        editorRole.Permissions.Should().Contain("crm.contacts.write");

        var viewerRole = result.Value.Single(r => r.Name == "Viewer");
        viewerRole.Permissions.Should().ContainSingle()
            .Which.Should().Be("crm.deals.read");
    }

    [Fact]
    public async Task GetUserRoles_WithUserHavingNoRoles_ReturnsEmptyList()
    {
        var userId = UserId.New();
        var orgId = OrganizationId.New();
        var orgUser = OrganizationUser.Create(userId, orgId);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new GetUserRolesHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetUserRolesHandler>.Instance);
        var result = await handler.Handle(
            new GetUserRolesQuery(userId.Value, orgId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRoles_WithNonExistentOrgUser_ReturnsFailure()
    {
        var handler = new GetUserRolesHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetUserRolesHandler>.Instance);
        var result = await handler.Handle(
            new GetUserRolesQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_in_org");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
