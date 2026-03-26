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

public sealed class GetRoleByIdTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetRoleByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task GetRoleById_WithExistingRole_ReturnsRoleDetailDto()
    {
        var role = Role.Create(_tenantId, "TestRole", "A test role description");
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        var handler = new GetRoleByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetRoleByIdHandler>.Instance);
        var result = await handler.Handle(new GetRoleByIdQuery(role.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(role.Id.Value);
        result.Value.Name.Should().Be("TestRole");
        result.Value.Description.Should().Be("A test role description");
        result.Value.IsSystemRole.Should().BeFalse();
        result.Value.IsActive.Should().BeTrue();
        result.Value.Permissions.Should().BeEmpty();
        result.Value.AssignedUserCount.Should().Be(0);
    }

    [Fact]
    public async Task GetRoleById_WithNonExistentRole_ReturnsFailure()
    {
        var handler = new GetRoleByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetRoleByIdHandler>.Instance);
        var result = await handler.Handle(new GetRoleByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_not_found");
    }

    [Fact]
    public async Task GetRoleById_WithRoleHavingPermissions_IncludesAllPermissions()
    {
        var role = Role.Create(_tenantId, "Editor", "Editor role");
        var perm1 = Permission.Create("crm", "contacts", "read");
        var perm2 = Permission.Create("crm", "contacts", "write");
        _dbContext.Permissions.AddRange(perm1, perm2);
        role.AssignPermission(perm1);
        role.AssignPermission(perm2);
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();

        var handler = new GetRoleByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetRoleByIdHandler>.Instance);
        var result = await handler.Handle(new GetRoleByIdQuery(role.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.Should().HaveCount(2);
        result.Value.Permissions.Select(p => p.Key).Should()
            .Contain("crm.contacts.read")
            .And.Contain("crm.contacts.write");
    }

    [Fact]
    public async Task GetRoleById_WithRoleAssignedToUsers_ReturnsCorrectUserCount()
    {
        var role = Role.Create(_tenantId, "Viewer", "Viewer role");
        _dbContext.Roles.Add(role);

        var orgUserId1 = OrganizationUserId.New();
        var orgUserId2 = OrganizationUserId.New();
        var orgUserId3 = OrganizationUserId.New();
        _dbContext.UserRoles.Add(UserRole.Create(orgUserId1, role.Id));
        _dbContext.UserRoles.Add(UserRole.Create(orgUserId2, role.Id));
        _dbContext.UserRoles.Add(UserRole.Create(orgUserId3, role.Id));
        await _dbContext.SaveChangesAsync();

        var handler = new GetRoleByIdHandler(
            _dbContext, _tenantAccessor,
            NullLogger<GetRoleByIdHandler>.Instance);
        var result = await handler.Handle(new GetRoleByIdQuery(role.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AssignedUserCount.Should().Be(3);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
