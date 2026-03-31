using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class GetTenantsQueryTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;

    public GetTenantsQueryTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _platformDb = new PlatformDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaginatedTenants()
    {
        // Arrange
        await _platformDb.Tenants.AddRangeAsync(
            Tenant.Create("Alpha", "alpha"),
            Tenant.Create("Beta", "beta"),
            Tenant.Create("Gamma", "gamma"));
        await _platformDb.SaveChangesAsync();

        // Act
        var handler = new GetTenantsHandler(_platformDb);
        var result = await handler.Handle(new GetTenantsQuery(1, 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_EmptyDb_ShouldReturnEmpty()
    {
        // Arrange
        var handler = new GetTenantsHandler(_platformDb);

        // Act
        var result = await handler.Handle(new GetTenantsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    public void Dispose() => _platformDb.Dispose();
}
