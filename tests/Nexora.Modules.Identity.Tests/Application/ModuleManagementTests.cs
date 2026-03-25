using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Application.Queries;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class ModuleManagementTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly IdentityDbContext _identityDb;
    private readonly TenantId _tenantId = TenantId.New();
    private readonly IModule _crmModule;
    private readonly IModule _identityModule;
    private readonly List<IModule> _modules;

    public ModuleManagementTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        _platformDb = new PlatformDbContext(options);

        var tenantAccessor = new TenantContextAccessor();
        tenantAccessor.SetTenant(_tenantId.Value.ToString());
        var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(dbName + "_identity").Options;
        _identityDb = new IdentityDbContext(identityOptions, tenantAccessor);

        // Create test tenant
        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.Activate();
        _platformDb.Tenants.Add(tenant);

        // Pre-install identity module
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "identity"));
        _platformDb.SaveChanges();

        // Mock registered modules
        _identityModule = Substitute.For<IModule>();
        _identityModule.Name.Returns("identity");
        _identityModule.Dependencies.Returns(new List<string>());

        _crmModule = Substitute.For<IModule>();
        _crmModule.Name.Returns("crm");
        _crmModule.Dependencies.Returns(new List<string> { "identity" });

        _modules = [_identityModule, _crmModule];
    }

    [Fact]
    public async Task InstallModule_ValidModule_ShouldInstall()
    {
        var handler = new InstallModuleHandler(_platformDb, _modules, NullLogger<InstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new InstallModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ModuleName.Should().Be("crm");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task InstallModule_AlreadyInstalled_ShouldReturnFailure()
    {
        var handler = new InstallModuleHandler(_platformDb, _modules, NullLogger<InstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new InstallModuleCommand(_tenantId.Value, "identity"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_already_installed");
    }

    [Fact]
    public async Task InstallModule_UnknownModule_ShouldReturnFailure()
    {
        var handler = new InstallModuleHandler(_platformDb, _modules, NullLogger<InstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new InstallModuleCommand(_tenantId.Value, "nonexistent"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_not_found");
    }

    [Fact]
    public async Task InstallModule_MissingDependency_ShouldReturnFailure()
    {
        // Remove identity so CRM's dependency is missing
        var identityModule = await _platformDb.TenantModules
            .FirstAsync(tm => tm.TenantId == _tenantId && tm.ModuleName == "identity");
        identityModule.Deactivate();
        await _platformDb.SaveChangesAsync();

        var handler = new InstallModuleHandler(_platformDb, _modules, NullLogger<InstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new InstallModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_dependency_missing");
    }

    [Fact]
    public async Task InstallModule_NonExistentTenant_ShouldReturnFailure()
    {
        var handler = new InstallModuleHandler(_platformDb, _modules, NullLogger<InstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new InstallModuleCommand(Guid.NewGuid(), "crm"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_tenant_not_found");
    }

    [Fact]
    public async Task UninstallModule_InstalledModule_ShouldDeactivate()
    {
        // Install CRM first
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "crm"));
        await _platformDb.SaveChangesAsync();

        var handler = new UninstallModuleHandler(_platformDb, _identityDb, _modules, NullLogger<UninstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new UninstallModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var module = await _platformDb.TenantModules
            .IgnoreQueryFilters()
            .FirstAsync(tm => tm.TenantId == _tenantId && tm.ModuleName == "crm");
        module.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UninstallModule_NotInstalled_ShouldReturnFailure()
    {
        var handler = new UninstallModuleHandler(_platformDb, _identityDb, _modules, NullLogger<UninstallModuleHandler>.Instance);
        var result = await handler.Handle(
            new UninstallModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_not_installed");
    }

    [Fact]
    public async Task UninstallModule_ShouldCallOnUninstallAsync()
    {
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "crm"));
        await _platformDb.SaveChangesAsync();

        var handler = new UninstallModuleHandler(_platformDb, _identityDb, _modules, NullLogger<UninstallModuleHandler>.Instance);
        await handler.Handle(
            new UninstallModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        await _crmModule.Received(1).OnUninstallAsync(
            Arg.Is<TenantInstallContext>(c => c.TenantId == _tenantId.Value.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTenantModules_ShouldReturnInstalledModules()
    {
        var handler = new GetTenantModulesHandler(_platformDb);
        var result = await handler.Handle(
            new GetTenantModulesQuery(_tenantId.Value), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().ContainSingle();
        result.Value[0].ModuleName.Should().Be("identity");
    }

    [Fact]
    public async Task GetTenantModules_MultipleModules_ShouldReturnAll()
    {
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "crm"));
        await _platformDb.SaveChangesAsync();

        var handler = new GetTenantModulesHandler(_platformDb);
        var result = await handler.Handle(
            new GetTenantModulesQuery(_tenantId.Value), CancellationToken.None);

        result.Value!.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _identityDb.Dispose();
        _platformDb.Dispose();
    }
}
