using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetOrganizationByIdTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetOrganizationByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingOrg_ShouldReturnDetail()
    {
        var org = Organization.Create(_tenantId, "Test Org", "test-org");
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var handler = new GetOrganizationByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetOrganizationByIdHandler>.Instance);
        var result = await handler.Handle(new GetOrganizationByIdQuery(org.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Test Org");
        result.Value.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithMembers_ShouldReturnCorrectCount()
    {
        var org = Organization.Create(_tenantId, "Org", "org");
        var user1 = User.Create(_tenantId, "kc-1", "a@t.com", "A", "B");
        var user2 = User.Create(_tenantId, "kc-2", "b@t.com", "C", "D");
        _dbContext.Organizations.Add(org);
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.OrganizationUsers.Add(OrganizationUser.Create(user1.Id, org.Id));
        _dbContext.OrganizationUsers.Add(OrganizationUser.Create(user2.Id, org.Id));
        await _dbContext.SaveChangesAsync();

        var handler = new GetOrganizationByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetOrganizationByIdHandler>.Instance);
        var result = await handler.Handle(new GetOrganizationByIdQuery(org.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonExistentOrg_ShouldReturnFailure()
    {
        var handler = new GetOrganizationByIdHandler(_dbContext, _tenantAccessor, NullLogger<GetOrganizationByIdHandler>.Instance);
        var result = await handler.Handle(new GetOrganizationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
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
