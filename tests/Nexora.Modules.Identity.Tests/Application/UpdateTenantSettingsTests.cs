using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class UpdateTenantSettingsTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly PlatformDbContext _platformDb;

    public UpdateTenantSettingsTests()
    {
        _platformDb = CreateContext();
    }

    private PlatformDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options);

    [Fact]
    public async Task Handle_ValidSettings_PersistsLocaleSettings()
    {
        var tenant = Tenant.Create("Test Corp", "test-corp");
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new UpdateTenantSettingsHandler(_platformDb, NullLogger<UpdateTenantSettingsHandler>.Instance);
        var command = new UpdateTenantSettingsCommand(tenant.Id.Value, "tr-TR", "TRY", "Europe/Istanbul", "tr");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        using var verify = CreateContext();
        var updated = await verify.Tenants.FirstAsync();
        var settings = updated.GetSettings();
        settings.DefaultLocale.Should().Be("tr-TR");
        settings.DefaultCurrency.Should().Be("TRY");
        settings.DefaultTimezone.Should().Be("Europe/Istanbul");
        settings.DefaultDocumentLanguage.Should().Be("tr");
    }

    [Fact]
    public async Task Handle_TenantNotFound_ReturnsFailure()
    {
        var handler = new UpdateTenantSettingsHandler(_platformDb, NullLogger<UpdateTenantSettingsHandler>.Instance);
        var command = new UpdateTenantSettingsCommand(Guid.NewGuid(), "en-US", "USD", "UTC", "en");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_not_found");
    }

    [Fact]
    public async Task Handle_OverwritesExistingSettings()
    {
        var tenant = Tenant.Create("Test Corp", "test-corp");
        tenant.UpdateSettings(new TenantSettings("en-US", "USD", "UTC", "en"));
        await _platformDb.Tenants.AddAsync(tenant);
        await _platformDb.SaveChangesAsync();

        var handler = new UpdateTenantSettingsHandler(_platformDb, NullLogger<UpdateTenantSettingsHandler>.Instance);
        var command = new UpdateTenantSettingsCommand(tenant.Id.Value, "tr-TR", "TRY", "Europe/Istanbul", "tr");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        using var verify = CreateContext();
        var updated = await verify.Tenants.FirstAsync();
        updated.GetSettings().DefaultLocale.Should().Be("tr-TR");
    }

    public void Dispose() => _platformDb.Dispose();
}
