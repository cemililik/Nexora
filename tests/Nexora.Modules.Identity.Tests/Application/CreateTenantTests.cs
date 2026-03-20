using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateTenantTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantSchemaManager _schemaManager;
    private readonly IKeycloakAdminService _keycloakAdmin;

    public CreateTenantTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(options);
        _schemaManager = Substitute.For<ITenantSchemaManager>();
        _keycloakAdmin = Substitute.For<IKeycloakAdminService>();

        _keycloakAdmin.CreateRealmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(0));
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateTenant()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var command = new CreateTenantCommand("Acme Corp", "acme-corp");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Acme Corp");
        result.Value.Slug.Should().Be("acme-corp");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_ShouldCallSchemaManager()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var command = new CreateTenantCommand("Test Tenant", "test-tenant");

        await handler.Handle(command, CancellationToken.None);

        await _schemaManager.Received(1).CreateSchemaAsync(
            Arg.Is<string>(s => s.StartsWith("tenant_")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ShouldReturnFailure()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        await handler.Handle(new CreateTenantCommand("First", "taken-slug"), CancellationToken.None);

        var result = await handler.Handle(
            new CreateTenantCommand("Second", "taken-slug"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_slug_taken");
    }

    [Fact]
    public async Task Handle_ShouldInstallIdentityModule()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        await handler.Handle(new CreateTenantCommand("Test", "test-module"), CancellationToken.None);

        var modules = await _platformDb.TenantModules.ToListAsync();
        modules.Should().ContainSingle();
        modules[0].ModuleName.Should().Be("identity");
    }

    [Fact]
    public async Task Handle_ShouldPersistTenant()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        await handler.Handle(new CreateTenantCommand("Persisted", "persisted"), CancellationToken.None);

        var count = await _platformDb.Tenants.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCreateKeycloakRealm()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        await handler.Handle(new CreateTenantCommand("Acme", "acme"), CancellationToken.None);

        await _keycloakAdmin.Received(1).CreateRealmAsync(
            "tenant-acme", "Acme", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSetRealmId()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var result = await handler.Handle(new CreateTenantCommand("Realm Test", "realm-test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RealmId.Should().Be("tenant-realm-test");
    }

    public void Dispose() => _platformDb.Dispose();
}
