using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;

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
        // Arrange
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("crm", "contacts", "read"),
            Permission.Create("identity", "users", "create"));
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_FilterByModule_ShouldReturnFiltered()
    {
        // Arrange
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("crm", "contacts", "read"),
            Permission.Create("crm", "contacts", "write"),
            Permission.Create("identity", "users", "create"));
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery("crm"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.All(p => p.Module == "crm").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FilterByTenantScope_ShouldExcludePlatformPermissions()
    {
        // Arrange
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("identity", "tenants", "read", scope: PermissionScope.Platform),
            Permission.Create("identity", "tenants", "manage", scope: PermissionScope.Platform),
            Permission.Create("identity", "users", "read"),
            Permission.Create("contacts", "contact", "read"));
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery(Scope: PermissionScope.Tenant), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.All(p => p.Scope == PermissionScope.Tenant).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FilterByPlatformScope_ShouldReturnOnlyPlatformPermissions()
    {
        // Arrange
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("identity", "tenants", "read", scope: PermissionScope.Platform),
            Permission.Create("identity", "tenants", "manage", scope: PermissionScope.Platform),
            Permission.Create("identity", "users", "read"),
            Permission.Create("contacts", "contact", "read"));
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery(Scope: PermissionScope.Platform), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.All(p => p.Scope == PermissionScope.Platform).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FilterByModuleAndScope_ShouldCombineFilters()
    {
        // Arrange
        await _dbContext.Permissions.AddRangeAsync(
            Permission.Create("identity", "tenants", "read", scope: PermissionScope.Platform),
            Permission.Create("identity", "users", "read"),
            Permission.Create("contacts", "contact", "read"));
        await _dbContext.SaveChangesAsync();

        // Act
        var handler = new GetPermissionsHandler(_dbContext);
        var result = await handler.Handle(new GetPermissionsQuery("identity", PermissionScope.Tenant), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Key.Should().Be("identity.users.read");
    }

    public void Dispose() => _dbContext.Dispose();
}
