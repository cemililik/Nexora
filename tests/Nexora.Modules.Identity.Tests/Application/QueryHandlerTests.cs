using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;

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
        await _platformDb.Tenants.AddRangeAsync(
            Tenant.Create("Alpha", "alpha"),
            Tenant.Create("Beta", "beta"),
            Tenant.Create("Gamma", "gamma"));
        await _platformDb.SaveChangesAsync();

        var handler = new GetTenantsHandler(_platformDb);
        var result = await handler.Handle(new GetTenantsQuery(1, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_EmptyDb_ShouldReturnEmpty()
    {
        var handler = new GetTenantsHandler(_platformDb);
        var result = await handler.Handle(new GetTenantsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    public void Dispose() => _platformDb.Dispose();
}

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

        var handler = new GetUsersHandler(_dbContext, _tenantAccessor);
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

        var handler = new GetUsersHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        result.Value!.Items.Should().ContainSingle();
        result.Value.Items[0].Email.Should().Be("mine@test.com");
    }

    public void Dispose() => _dbContext.Dispose();
}

public sealed class GetRolesQueryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly TenantId _tenantId = TenantId.New();

    public GetRolesQueryTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        ((TenantContextAccessor)_tenantAccessor).SetTenant(_tenantId.Value.ToString());

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new IdentityDbContext(options, _tenantAccessor);
    }

    [Fact]
    public async Task Handle_ShouldReturnRolesWithPermissions()
    {
        var permission = Permission.Create("crm", "contacts", "read");
        await _dbContext.Permissions.AddAsync(permission);

        var role = Role.Create(_tenantId, "Editor");
        role.AssignPermission(permission);
        await _dbContext.Roles.AddAsync(role);
        await _dbContext.SaveChangesAsync();

        var handler = new GetRolesHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value![0].Name.Should().Be("Editor");
        result.Value[0].Permissions.Should().Contain("crm.contacts.read");
    }

    [Fact]
    public async Task Handle_EmptyDb_ShouldReturnEmptyList()
    {
        var handler = new GetRolesHandler(_dbContext, _tenantAccessor);
        var result = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}

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
