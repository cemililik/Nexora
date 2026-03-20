using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateUserStatusTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();

    public UpdateUserStatusTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _platformDb = new PlatformDbContext(platformOptions);

        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.SetRealmId("tenant-test");
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task Handle_Deactivate_ShouldSetInactive()
    {
        var user = User.Create(_tenantId, "kc-1", "a@t.com", "A", "B");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserStatusHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateUserStatusCommand(user.Id.Value, "deactivate"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Users.FindAsync(user.Id);
        updated!.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public async Task Handle_Activate_ShouldSetActive()
    {
        var user = User.Create(_tenantId, "kc-1", "a@t.com", "A", "B");
        user.Deactivate();
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserStatusHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateUserStatusCommand(user.Id.Value, "activate"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await _dbContext.Users.FindAsync(user.Id);
        updated!.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task Handle_Deactivate_ShouldSyncKeycloak()
    {
        var user = User.Create(_tenantId, "kc-1", "a@t.com", "A", "B");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserStatusHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserStatusHandler>.Instance);
        await handler.Handle(
            new UpdateUserStatusCommand(user.Id.Value, "deactivate"), CancellationToken.None);

        await _keycloakAdmin.Received(1).DisableUserAsync(
            "tenant-test", "kc-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentUser_ShouldReturnFailure()
    {
        var handler = new UpdateUserStatusHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserStatusHandler>.Instance);
        var result = await handler.Handle(
            new UpdateUserStatusCommand(Guid.NewGuid(), "activate"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_not_found");
    }

    public void Dispose() { _dbContext.Dispose(); _platformDb.Dispose(); }

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
