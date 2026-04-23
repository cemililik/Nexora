using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

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
    public async Task Handle_ExistingTenant_ReturnsDefaultLocaleSettings()
    {
        var tenant = Tenant.Create("Test Corp", "test-corp");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var result = await handler.Handle(new GetTenantByIdQuery(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Tenant with no Settings → should fall back to platform defaults
        result.Value!.DefaultLocale.Should().Be("en-US");
        result.Value.DefaultCurrency.Should().Be("USD");
        result.Value.DefaultTimezone.Should().Be("UTC");
        result.Value.DefaultDocumentLanguage.Should().Be("en");
    }

    [Fact]
    public async Task Handle_TenantWithCustomSettings_ReturnsConfiguredLocale()
    {
        var tenant = Tenant.Create("TR Corp", "tr-corp");
        tenant.UpdateSettings(new TenantSettings("tr-TR", "TRY", "Europe/Istanbul", "tr"));
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new GetTenantByIdHandler(_platformDb, NullLogger<GetTenantByIdHandler>.Instance);
        var result = await handler.Handle(new GetTenantByIdQuery(tenant.Id.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultLocale.Should().Be("tr-TR");
        result.Value.DefaultCurrency.Should().Be("TRY");
        result.Value.DefaultTimezone.Should().Be("Europe/Istanbul");
        result.Value.DefaultDocumentLanguage.Should().Be("tr");
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
