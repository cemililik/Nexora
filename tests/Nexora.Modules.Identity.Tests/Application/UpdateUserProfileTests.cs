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

public sealed class UpdateUserProfileTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();

    public UpdateUserProfileTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _platformDb = new PlatformDbContext(platformOptions);

        // Seed tenant with realm
        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.SetRealmId("tenant-test");
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldUpdateProfile()
    {
        var user = User.Create(_tenantId, "kc-1", "john@test.com", "John", "Doe");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserProfileHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserProfileHandler>.Instance);
        var command = new UpdateUserProfileCommand(user.Id.Value, "Jane", "Smith", "+1234567890");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FirstName.Should().Be("Jane");
        result.Value.LastName.Should().Be("Smith");
        result.Value.Phone.Should().Be("+1234567890");
    }

    [Fact]
    public async Task Handle_ShouldSyncToKeycloak()
    {
        var user = User.Create(_tenantId, "kc-1", "sync@test.com", "A", "B");
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var handler = new UpdateUserProfileHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserProfileHandler>.Instance);
        await handler.Handle(
            new UpdateUserProfileCommand(user.Id.Value, "New", "Name", null), CancellationToken.None);

        await _keycloakAdmin.Received(1).UpdateUserAsync(
            "tenant-test", "kc-1", "sync@test.com", "New", "Name", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonExistentUser_ShouldReturnFailure()
    {
        var handler = new UpdateUserProfileHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<UpdateUserProfileHandler>.Instance);
        var result = await handler.Handle(
            new UpdateUserProfileCommand(Guid.NewGuid(), "A", "B", null), CancellationToken.None);

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
