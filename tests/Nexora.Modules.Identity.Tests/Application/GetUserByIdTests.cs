using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetUserByIdTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetUserByIdTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ExistingUser_ShouldReturnDetail()
    {
        var user = User.Create(_tenantId, "kc-1", "john@test.com", "John", "Doe");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetUserByIdQuery(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("john@test.com");
        result.Value.Organizations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithOrganizations_ShouldReturnMemberships()
    {
        var user = User.Create(_tenantId, "kc-1", "org@test.com", "A", "B");
        var org = Organization.Create(_tenantId, "Org", "org");
        _dbContext.Users.Add(user);
        _dbContext.Organizations.Add(org);
        _dbContext.OrganizationUsers.Add(OrganizationUser.Create(user.Id, org.Id, true));
        await _dbContext.SaveChangesAsync();

        var handler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetUserByIdQuery(user.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Organizations.Should().ContainSingle();
        result.Value.Organizations[0].OrganizationName.Should().Be("Org");
        result.Value.Organizations[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentUser_ShouldReturnFailure()
    {
        var handler = new GetUserByIdHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    [Fact]
    public async Task Handle_GetCurrentUser_ShouldResolveByKeycloakId()
    {
        var user = User.Create(_tenantId, "kc-sub-123", "me@test.com", "Me", "User");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetCurrentUserQuery("kc-sub-123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("me@test.com");
    }

    [Fact]
    public async Task Handle_GetCurrentUser_UnknownKeycloakId_ShouldReturnFailure()
    {
        var handler = new GetCurrentUserHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetCurrentUserQuery("unknown-id"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
