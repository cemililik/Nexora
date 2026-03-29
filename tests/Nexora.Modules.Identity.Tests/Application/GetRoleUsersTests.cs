using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetRoleUsersTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Organization _org;
    private readonly Role _role;

    public GetRoleUsersTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);

        _org = Organization.Create(_tenantId, "Test Org", "test-org");
        _role = Role.Create(_tenantId, "Admin");
        _dbContext.Organizations.Add(_org);
        _dbContext.Roles.Add(_role);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Handle_RoleWithUsers_ReturnsPagedList()
    {
        // Arrange: create user, org membership, and role assignment
        var user = User.Create(_tenantId, "kc-1", "john@test.com", "John", "Doe");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var orgUser = OrganizationUser.Create(user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var userRole = UserRole.Create(orgUser.Id, _role.Id);
        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync();

        var handler = new GetRoleUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetRoleUsersHandler>.Instance);
        var query = new GetRoleUsersQuery(_role.Id.Value);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Email.Should().Be("john@test.com");
        result.Value.Items[0].FirstName.Should().Be("John");
        result.Value.Items[0].LastName.Should().Be("Doe");
        result.Value.Items[0].OrganizationName.Should().Be("Test Org");
        result.Value.Items[0].AssignedAt.Should().BeCloseTo(userRole.AssignedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_RoleWithNoUsers_ReturnsEmptyList()
    {
        var handler = new GetRoleUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetRoleUsersHandler>.Instance);
        var query = new GetRoleUsersQuery(_role.Id.Value);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonExistentRole_ReturnsFailure()
    {
        var handler = new GetRoleUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetRoleUsersHandler>.Instance);
        var query = new GetRoleUsersQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_role_not_found");
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectPage()
    {
        // Arrange: create 3 users with role assignments
        for (var i = 1; i <= 3; i++)
        {
            var user = User.Create(_tenantId, $"kc-{i}", $"user{i}@test.com", $"First{i}", $"Last{i}");
            _dbContext.Users.Add(user);

            var orgUser = OrganizationUser.Create(user.Id, _org.Id);
            _dbContext.OrganizationUsers.Add(orgUser);

            var userRole = UserRole.Create(orgUser.Id, _role.Id);
            _dbContext.UserRoles.Add(userRole);
        }

        await _dbContext.SaveChangesAsync();

        var handler = new GetRoleUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetRoleUsersHandler>.Instance);

        // Act: request page 2 with page size 2
        var query = new GetRoleUsersQuery(_role.Id.Value, Page: 2, PageSize: 2);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        result.Value.Items.Should().HaveCount(1);
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
