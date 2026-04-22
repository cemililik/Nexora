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
using NSubstitute.ExceptionExtensions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateUserTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly IKeycloakAdminService _keycloakAdmin;
    private readonly TenantId _tenantId = TenantId.New();

    public CreateUserTests()
    {
        _tenantAccessor = CreateTenantAccessor(_tenantId);
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        _keycloakAdmin.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("kc-generated-id");

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new IdentityDbContext(identityOptions, _tenantAccessor);

        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(platformOptions);

        // Seed a tenant with a realm
        var tenant = Tenant.Create("Test Tenant", "test");
        // Use reflection to set the Id to match our tenantId
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.SetRealmId("tenant-test");
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateUser()
    {
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var command = new CreateUserCommand("john@example.com", "John", "Doe", "TempPass1!");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("john@example.com");
        result.Value.FirstName.Should().Be("John");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ShouldCreateKeycloakUser()
    {
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var command = new CreateUserCommand("jane@example.com", "Jane", "Smith", "TempPass1!");

        await handler.Handle(command, CancellationToken.None);

        await _keycloakAdmin.Received(1).CreateUserAsync(
            "tenant-test",
            "jane@example.com",
            "jane@example.com",
            "Jane",
            "Smith",
            "TempPass1!",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldReturnFailure()
    {
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        await handler.Handle(
            new CreateUserCommand("taken@example.com", "A", "B", "TempPass1!"), CancellationToken.None);

        var result = await handler.Handle(
            new CreateUserCommand("taken@example.com", "C", "D", "TempPass1!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_email_taken");
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldNotCallKeycloak()
    {
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        await handler.Handle(
            new CreateUserCommand("dup@example.com", "A", "B", "TempPass1!"), CancellationToken.None);

        _keycloakAdmin.ClearReceivedCalls();

        await handler.Handle(
            new CreateUserCommand("dup@example.com", "C", "D", "TempPass1!"), CancellationToken.None);

        await _keycloakAdmin.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TenantWithoutRealm_ShouldReturnFailure()
    {
        // Create a tenant without realm
        var noRealmTenantId = TenantId.New();
        var noRealmAccessor = CreateTenantAccessor(noRealmTenantId);

        var tenant = Tenant.Create("No Realm", "no-realm");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, noRealmTenantId);
        _platformDb.Tenants.Add(tenant);
        await _platformDb.SaveChangesAsync();

        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var identityDb = new IdentityDbContext(identityOptions, noRealmAccessor);

        var handler = new CreateUserHandler(identityDb, _platformDb, noRealmAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        var result = await handler.Handle(
            new CreateUserCommand("test@test.com", "T", "U", "TempPass1!"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_realm_not_configured");
    }

    [Fact]
    public async Task Handle_ShouldPersistUser()
    {
        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, _keycloakAdmin, NullLogger<CreateUserHandler>.Instance);
        await handler.Handle(
            new CreateUserCommand("p@test.com", "P", "T", "TempPass1!"), CancellationToken.None);

        var count = await _dbContext.Users.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_KeycloakThrowsHttpRequestException_ReturnsFailure()
    {
        // Arrange — configure Keycloak to throw a non-conflict HTTP error
        var failingKeycloak = Substitute.For<IKeycloakAdminService>();
        failingKeycloak.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Keycloak is unreachable"));

        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, failingKeycloak,
            NullLogger<CreateUserHandler>.Instance);
        var command = new CreateUserCommand("fail@example.com", "Fail", "User", "TempPass1!");

        // Act — handler catches the exception and returns Result.Failure
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_user_create_failed");
    }

    [Fact]
    public async Task Handle_KeycloakThrowsHttpRequestException_ShouldNotPersistUser()
    {
        // Arrange — configure Keycloak to throw
        var failingKeycloak = Substitute.For<IKeycloakAdminService>();
        failingKeycloak.CreateUserAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Keycloak is down"));

        var handler = new CreateUserHandler(_dbContext, _platformDb, _tenantAccessor, failingKeycloak,
            NullLogger<CreateUserHandler>.Instance);
        var command = new CreateUserCommand("nopersist@example.com", "No", "Persist", "TempPass1!");

        // Act — ignore the exception
        try { await handler.Handle(command, CancellationToken.None); }
        catch (HttpRequestException) { }

        // Assert — user should NOT be persisted since exception occurred before save
        var count = await _dbContext.Users.CountAsync();
        count.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _platformDb.Dispose();
    }

    private static ITenantContextAccessor CreateTenantAccessor(TenantId tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId.Value.ToString());
        return accessor;
    }
}
