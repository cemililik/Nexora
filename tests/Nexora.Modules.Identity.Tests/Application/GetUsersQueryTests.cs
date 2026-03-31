using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetUsersQueryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetUsersQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(_tenantId.Value.ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ShouldReturnUsersForTenant()
    {
        await _dbContext.Users.AddRangeAsync(
            User.Create(_tenantId, "kc-1", "a@test.com", "Alice", "Smith"),
            User.Create(_tenantId, "kc-2", "b@test.com", "Bob", "Jones"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetUsersHandler>.Instance);
        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldIsolateTenants()
    {
        var otherTenantId = TenantId.New();
        await _dbContext.Users.AddAsync(
            User.Create(otherTenantId, "kc-3", "other@test.com", "Other", "User"));
        await _dbContext.Users.AddAsync(
            User.Create(_tenantId, "kc-1", "mine@test.com", "My", "User"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetUsersHandler(_dbContext, _tenantAccessor, NullLogger<GetUsersHandler>.Instance);
        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].Email.Should().Be("mine@test.com");
    }

    public void Dispose() => _dbContext.Dispose();
}
