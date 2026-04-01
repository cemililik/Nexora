using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetTenantByIdQueryTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;

    public GetTenantByIdQueryTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _platformDb = new PlatformDbContext(options);
    }

    [Fact]
    public async Task Handle_ExistingTenant_ShouldReturnDetail()
    {
        var tenant = Tenant.Create("Test Corp", "test-corp");
        await _platformDb.Tenants.AddAsync(tenant);
        var module = TenantModule.Create(tenant.Id, "identity");
        await _platformDb.TenantModules.AddAsync(module);
        await _platformDb.SaveChangesAsync();

        var handler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var result = await handler.Handle(new GetTenantByIdQuery(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Test Corp");
        result.Value.InstalledModules.Should().Contain("identity");
    }

    [Fact]
    public async Task Handle_NotFound_ShouldReturnFailure()
    {
        var handler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var result = await handler.Handle(new GetTenantByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_not_found");
    }

    public void Dispose() => _platformDb.Dispose();
}
