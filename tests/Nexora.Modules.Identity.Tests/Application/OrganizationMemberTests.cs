using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class OrganizationMemberTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly Organization _org;
    private readonly User _user;

    public OrganizationMemberTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);

        _org = Organization.Create(_tenantId, "Test Org", "test-org");
        _user = User.Create(_tenantId, "kc-1", "john@test.com", "John", "Doe");
        _dbContext.Organizations.Add(_org);
        _dbContext.Users.Add(_user);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task AddMember_ValidCommand_ShouldCreateMembership()
    {
        var handler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        var command = new AddOrganizationMemberCommand(_org.Id.Value, _user.Id.Value);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("john@test.com");
        result.Value.FirstName.Should().Be("John");
    }

    [Fact]
    public async Task AddMember_WithDefault_ShouldSetDefaultOrg()
    {
        var handler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        var command = new AddOrganizationMemberCommand(_org.Id.Value, _user.Id.Value, true);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsDefaultOrg.Should().BeTrue();
    }

    [Fact]
    public async Task AddMember_DuplicateMembership_ShouldReturnFailure()
    {
        var handler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        await handler.Handle(new AddOrganizationMemberCommand(_org.Id.Value, _user.Id.Value), CancellationToken.None);

        var result = await handler.Handle(
            new AddOrganizationMemberCommand(_org.Id.Value, _user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_already_member");
    }

    [Fact]
    public async Task AddMember_NonExistentOrg_ShouldReturnFailure()
    {
        var handler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        var result = await handler.Handle(
            new AddOrganizationMemberCommand(Guid.NewGuid(), _user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_org_not_found");
    }

    [Fact]
    public async Task AddMember_NonExistentUser_ShouldReturnFailure()
    {
        var handler = new AddOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<AddOrganizationMemberHandler>.Instance);
        var result = await handler.Handle(
            new AddOrganizationMemberCommand(_org.Id.Value, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task RemoveMember_ValidCommand_ShouldRemoveMembership()
    {
        // Add first
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new RemoveOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<RemoveOrganizationMemberHandler>.Instance);
        var result = await handler.Handle(
            new RemoveOrganizationMemberCommand(_org.Id.Value, _user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var count = await _dbContext.OrganizationUsers.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveMember_NotAMember_ShouldReturnFailure()
    {
        var handler = new RemoveOrganizationMemberHandler(_dbContext, _tenantAccessor, NullLogger<RemoveOrganizationMemberHandler>.Instance);
        var result = await handler.Handle(
            new RemoveOrganizationMemberCommand(_org.Id.Value, _user.Id.Value), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_member");
    }

    [Fact]
    public async Task GetMembers_ShouldReturnPagedResult()
    {
        var orgUser = OrganizationUser.Create(_user.Id, _org.Id);
        _dbContext.OrganizationUsers.Add(orgUser);
        await _dbContext.SaveChangesAsync();

        var handler = new GetOrganizationMembersHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(
            new GetOrganizationMembersQuery(_org.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Email.Should().Be("john@test.com");
    }

    [Fact]
    public async Task GetMembers_NonExistentOrg_ShouldReturnFailure()
    {
        var handler = new GetOrganizationMembersHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(
            new GetOrganizationMembersQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_org_not_found");
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
