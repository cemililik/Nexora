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

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>Integration tests verifying Keycloak synchronization during user and tenant operations.</summary>
public sealed class KeycloakSyncIntegrationTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly ITenantSchemaManager _schemaManager;
    private readonly TenantId _tenantId = TenantId.New();

    public KeycloakSyncIntegrationTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();
        _schemaManager = Substitute.For<ITenantSchemaManager>();

        _keycloakAdmin.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("kc-user-id-123");

        _keycloakAdmin.CreateRealmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(0));

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(platformOptions);

        TenantSeeder.SeedTenant(_platformDb, _tenantId, "KC Test Tenant", "kc-test", "tenant-kc-test");
    }

    [Fact]
    public async Task CreateUser_ShouldCallKeycloakCreateUser()
    {
        // Arrange
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateUserCommand("kc-user@example.com", "Keycloak", "User", "SecurePass1!"), CancellationToken.None);

        // Assert: operation succeeded
        result.IsSuccess.Should().BeTrue();

        // Assert: Keycloak was called with the correct realm and user details
        await _keycloakAdmin.Received(1).CreateUserAsync(
            "tenant-kc-test",
            "kc-user@example.com",
            "kc-user@example.com",
            "Keycloak",
            "User",
            "SecurePass1!",
            Arg.Any<CancellationToken>());

        // Assert: the Keycloak user ID is stored on the user entity
        var user = await _dbContext.Users.FirstOrDefaultAsync();
        user.Should().NotBeNull();
        user!.KeycloakUserId.Should().Be("kc-user-id-123");
    }

    [Fact]
    public async Task DeleteUser_ShouldCallKeycloakDisableUser()
    {
        // Arrange: create a user first
        var createHandler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateUserCommand("to-disable@example.com", "Disable", "Me", "TempPass1!"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var userId = createResult.Value!.Id;

        _keycloakAdmin.ClearReceivedCalls();

        // Use a different UserId for tenant context to avoid self-delete prevention
        var deleteAccessor = CreateTenantAccessor(_tenantId, userId: "different-kc-user");

        // Act: delete the user
        var deleteHandler = new DeleteUserHandler(_dbContext, _platformDb, deleteAccessor, _keycloakAdmin, NullLogger<DeleteUserHandler>.Instance);
        var deleteResult = await deleteHandler.Handle(
            new DeleteUserCommand(userId), CancellationToken.None);

        // Assert
        deleteResult.IsSuccess.Should().BeTrue();

        // Assert: Keycloak DisableUser was called
        await _keycloakAdmin.Received(1).DisableUserAsync(
            "tenant-kc-test",
            "kc-user-id-123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTenant_ShouldCallCreateRealm()
    {
        // Arrange: use a fresh PlatformDbContext to avoid conflicts with seeded tenant
        var freshPlatformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var freshPlatformDb = new PlatformDbContext(freshPlatformOptions);
        var freshKeycloak = Substitute.For<IKeycloakAdminService>();

        freshKeycloak.CreateRealmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(0));

        var handler = new CreateTenantHandler(freshPlatformDb, _schemaManager, freshKeycloak, NullLogger<CreateTenantHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new CreateTenantCommand("New Realm Org", "new-realm-org"), CancellationToken.None);

        // Assert: operation succeeded
        result.IsSuccess.Should().BeTrue();

        // Assert: Keycloak CreateRealm was called with the correct realm name
        await freshKeycloak.Received(1).CreateRealmAsync(
            "tenant-new-realm-org",
            "New Realm Org",
            Arg.Any<CancellationToken>());

        // Assert: the realm ID is stored on the tenant
        result.Value!.RealmId.Should().Be("tenant-new-realm-org");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _platformDb.Dispose();
    }

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId, string? userId = null)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString(), userId: userId);
        return accessor;
    }
}
