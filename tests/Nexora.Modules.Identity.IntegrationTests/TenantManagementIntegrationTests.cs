using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Nexora.Modules.Identity.IntegrationTests;

/// <summary>Integration tests for tenant management flows: create, query, and status transitions.</summary>
public sealed class TenantManagementIntegrationTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantSchemaManager _schemaManager;
    private readonly IKeycloakAdminService _keycloakAdmin;

    public TenantManagementIntegrationTests()
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
    public async Task CreateTenant_ThenGetById_ShouldReturn()
    {
        // Arrange & Act: create a tenant
        var createHandler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateTenantCommand("Acme Corp", "acme-corp"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var tenantId = createResult.Value!.Id;

        // Act: query by ID
        var queryHandler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var queryResult = await queryHandler.Handle(
            new GetTenantByIdQuery(tenantId), CancellationToken.None);

        // Assert
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value!.Name.Should().Be("Acme Corp");
        queryResult.Value.Slug.Should().Be("acme-corp");
        queryResult.Value.Status.Should().Be("Active");
        queryResult.Value.RealmId.Should().Be("tenant-acme-corp");
        queryResult.Value.InstalledModules.Should().Contain("identity");
    }

    [Fact]
    public async Task UpdateTenantStatus_Activate_ShouldChangeStatus()
    {
        // Arrange: create a tenant (starts as Trial, then gets Activated during creation)
        var createHandler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateTenantCommand("Suspend First", "suspend-first"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var tenantId = createResult.Value!.Id;

        // First suspend it so we can test activation
        var statusHandler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var suspendResult = await statusHandler.Handle(
            new UpdateTenantStatusCommand(tenantId, "suspend"), CancellationToken.None);

        suspendResult.IsSuccess.Should().BeTrue();

        // Act: activate the suspended tenant
        var activateResult = await statusHandler.Handle(
            new UpdateTenantStatusCommand(tenantId, "activate"), CancellationToken.None);

        // Assert
        activateResult.IsSuccess.Should().BeTrue();

        var queryHandler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var queryResult = await queryHandler.Handle(
            new GetTenantByIdQuery(tenantId), CancellationToken.None);

        queryResult.Value!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateTenantStatus_Suspend_ShouldChangeStatus()
    {
        // Arrange: create a tenant (gets auto-activated during creation)
        var createHandler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);
        var createResult = await createHandler.Handle(
            new CreateTenantCommand("To Suspend", "to-suspend"), CancellationToken.None);

        createResult.IsSuccess.Should().BeTrue();
        var tenantId = createResult.Value!.Id;

        // Act: suspend
        var statusHandler = new UpdateTenantStatusHandler(_platformDb, NullLogger<UpdateTenantStatusHandler>.Instance);
        var suspendResult = await statusHandler.Handle(
            new UpdateTenantStatusCommand(tenantId, "suspend"), CancellationToken.None);

        // Assert
        suspendResult.IsSuccess.Should().BeTrue();

        var queryHandler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var queryResult = await queryHandler.Handle(
            new GetTenantByIdQuery(tenantId), CancellationToken.None);

        queryResult.Value!.Status.Should().Be("Suspended");
    }

    [Fact]
    public async Task GetTenants_ShouldReturnPaginatedList()
    {
        // Arrange: create multiple tenants
        var createHandler = new CreateTenantHandler(_platformDb, _schemaManager, _keycloakAdmin, NullLogger<CreateTenantHandler>.Instance);

        await createHandler.Handle(new CreateTenantCommand("Alpha Inc", "alpha-inc"), CancellationToken.None);
        await createHandler.Handle(new CreateTenantCommand("Beta LLC", "beta-llc"), CancellationToken.None);
        await createHandler.Handle(new CreateTenantCommand("Gamma Co", "gamma-co"), CancellationToken.None);

        // Act: query first page with page size 2
        var queryHandler = new GetTenantsHandler(_platformDb);
        var result = await queryHandler.Handle(
            new GetTenantsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCount.Should().Be(3);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(2);

        // Act: query second page
        var page2Result = await queryHandler.Handle(
            new GetTenantsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        // Assert
        page2Result.IsSuccess.Should().BeTrue();
        page2Result.Value!.Items.Should().HaveCount(1);
    }

    public void Dispose() => _platformDb.Dispose();
}
