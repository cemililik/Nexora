using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class CreateTenantTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantSchemaManager _schemaManager;

    public CreateTenantTests()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _platformDb = new PlatformDbContext(options);
        _schemaManager = Substitute.For<ITenantSchemaManager>();
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldCreateTenant()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager);
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
        var handler = new CreateTenantHandler(_platformDb, _schemaManager);
        var command = new CreateTenantCommand("Test Tenant", "test-tenant");

        await handler.Handle(command, CancellationToken.None);

        await _schemaManager.Received(1).CreateSchemaAsync(
            Arg.Is<string>(s => s.StartsWith("tenant_")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateSlug_ShouldReturnFailure()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager);
        await handler.Handle(new CreateTenantCommand("First", "taken-slug"), CancellationToken.None);

        var result = await handler.Handle(
            new CreateTenantCommand("Second", "taken-slug"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_slug_taken");
    }

    [Fact]
    public async Task Handle_ShouldInstallIdentityModule()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager);
        await handler.Handle(new CreateTenantCommand("Test", "test-module"), CancellationToken.None);

        var modules = await _platformDb.TenantModules.ToListAsync();
        modules.Should().ContainSingle();
        modules[0].ModuleName.Should().Be("identity");
    }

    [Fact]
    public async Task Handle_ShouldPersistTenant()
    {
        var handler = new CreateTenantHandler(_platformDb, _schemaManager);
        await handler.Handle(new CreateTenantCommand("Persisted", "persisted"), CancellationToken.None);

        var count = await _platformDb.Tenants.CountAsync();
        count.Should().Be(1);
    }

    public void Dispose() => _platformDb.Dispose();
}
