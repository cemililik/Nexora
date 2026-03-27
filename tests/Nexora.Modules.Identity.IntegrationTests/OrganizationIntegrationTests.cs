using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>Integration tests for organization flows: create, add/remove members, and soft delete.</summary>
public sealed class OrganizationIntegrationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();

    public OrganizationIntegrationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        _keycloakAdmin.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("kc-generated-id");

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(platformOptions);

        TenantSeeder.SeedTenant(_platformDb, _tenantId, "Test Tenant", "test", "tenant-test");
    }

    [Fact]
    public async Task CreateOrganization_ThenAddMember_ShouldAssociate()
    {
        // Arrange: create an organization
        var createOrgHandler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var orgResult = await createOrgHandler.Handle(
            new CreateOrganizationCommand("Engineering", "engineering"), CancellationToken.None);

        orgResult.IsSuccess.Should().BeTrue();
        var orgId = orgResult.Value!.Id;

        // Arrange: create a user
        var createUserHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var userResult = await createUserHandler.Handle(
            new CreateUserCommand("member@example.com", "Team", "Member", "TempPass1!"), CancellationToken.None);

        userResult.IsSuccess.Should().BeTrue();
        var userId = userResult.Value!.Id;

        // Act: add user as member of the organization
        var addMemberHandler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        var memberResult = await addMemberHandler.Handle(
            new AddOrganizationMemberCommand(orgId, userId, IsDefault: true), CancellationToken.None);

        // Assert
        memberResult.IsSuccess.Should().BeTrue();
        memberResult.Value!.UserId.Should().Be(userId);
        memberResult.Value.Email.Should().Be("member@example.com");
        memberResult.Value.IsDefaultOrg.Should().BeTrue();

        // Verify through query
        var orgQueryHandler = new GetOrganizationByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetOrganizationByIdHandler>.Instance);
        var orgQueryResult = await orgQueryHandler.Handle(
            new GetOrganizationByIdQuery(orgId), CancellationToken.None);

        orgQueryResult.IsSuccess.Should().BeTrue();
        orgQueryResult.Value!.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task RemoveMember_ShouldDisassociate()
    {
        // Arrange: create org and user, then add membership
        var createOrgHandler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var orgResult = await createOrgHandler.Handle(
            new CreateOrganizationCommand("Sales", "sales"), CancellationToken.None);

        orgResult.IsSuccess.Should().BeTrue();
        var orgId = orgResult.Value!.Id;

        var createUserHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var userResult = await createUserHandler.Handle(
            new CreateUserCommand("removable@example.com", "Remove", "Me", "TempPass1!"), CancellationToken.None);

        userResult.IsSuccess.Should().BeTrue();
        var userId = userResult.Value!.Id;

        var addMemberHandler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        await addMemberHandler.Handle(
            new AddOrganizationMemberCommand(orgId, userId), CancellationToken.None);

        // Act: remove the member
        var removeHandler = new RemoveOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<RemoveOrganizationMemberHandler>.Instance);
        var removeResult = await removeHandler.Handle(
            new RemoveOrganizationMemberCommand(orgId, userId), CancellationToken.None);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();

        // Verify: membership no longer exists
        var membership = await _dbContext.OrganizationUsers
            .FirstOrDefaultAsync(ou => ou.OrganizationId == OrganizationId.From(orgId) && ou.UserId == UserId.From(userId));

        membership.Should().BeNull();

        // Verify: org member count is 0
        var orgQueryHandler = new GetOrganizationByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetOrganizationByIdHandler>.Instance);
        var orgQueryResult = await orgQueryHandler.Handle(
            new GetOrganizationByIdQuery(orgId), CancellationToken.None);

        orgQueryResult.IsSuccess.Should().BeTrue();
        orgQueryResult.Value!.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteOrganization_ShouldSoftDelete()
    {
        // Arrange: create an organization
        var createOrgHandler = new CreateOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<CreateOrganizationHandler>.Instance);
        var orgResult = await createOrgHandler.Handle(
            new CreateOrganizationCommand("Deprecated Dept", "deprecated-dept"), CancellationToken.None);

        orgResult.IsSuccess.Should().BeTrue();
        var orgId = orgResult.Value!.Id;

        // Act: delete (deactivate) the organization
        var deleteHandler = new DeleteOrganizationHandler(_dbContext, _tenantAccessor, NullLogger<DeleteOrganizationHandler>.Instance);
        var deleteResult = await deleteHandler.Handle(
            new DeleteOrganizationCommand(orgId), CancellationToken.None);

        // Assert: deletion succeeded
        deleteResult.IsSuccess.Should().BeTrue();

        // Assert: organization is deactivated
        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == OrganizationId.From(orgId));

        org.Should().NotBeNull();
        org!.IsActive.Should().BeFalse();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _platformDb.Dispose();
    }

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
