using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetPermissionsQueryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;

    public GetPermissionsQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(Guid.NewGuid().ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_NoFilter_ShouldReturnAll()
    {
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("crm", "contacts", "read"),
            Permission.Create("identity", "users", "create"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByModule_ShouldReturnFiltered()
    {
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("crm", "contacts", "read"),
            Permission.Create("crm", "contacts", "write"),
            Permission.Create("identity", "users", "create"));
        await _dbContext.SaveChangesAsync();

        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery("crm"), CancellationToken.None);

        result.Value.Should().HaveCount(2);
        result.Value!.All(p => p.Module == "crm").Should().BeTrue();
    }

    public void Dispose() => _dbContext.Dispose();
}
