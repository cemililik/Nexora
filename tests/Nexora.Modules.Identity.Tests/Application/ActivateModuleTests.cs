using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class ActivateModuleTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly ITenantSchemaManager _schemaManager;
    private readonly TenantId _tenantId = TenantId.New();

    public ActivateModuleTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        _platformDb = new PlatformDbContext(options);

        _schemaManager = Substitute.For<ITenantSchemaManager>();

        // Create and seed tenant
        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.Activate();
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task ActivateModule_WithDeactivatedModule_ActivatesAndReturnsSuccess()
    {
        // Arrange
        var module = TenantModule.Create(_tenantId, "crm");
        module.Deactivate();
        _platformDb.TenantModules.Add(module);
        await _platformDb.SaveChangesAsync();

        var handler = new ActivateModuleHandler(_platformDb, _schemaManager, NullLogger<ActivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new ActivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updated = await _platformDb.TenantModules
            .FirstAsync(tm => tm.TenantId == _tenantId && tm.ModuleName == "crm");
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateModule_WithAlreadyActiveModule_ReturnsFailure()
    {
        // Arrange
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "crm"));
        await _platformDb.SaveChangesAsync();

        var handler = new ActivateModuleHandler(_platformDb, _schemaManager, NullLogger<ActivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new ActivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_already_active");
    }

    [Fact]
    public async Task ActivateModule_WithNotInstalledModule_ReturnsFailure()
    {
        // Arrange — no module seeded
        var handler = new ActivateModuleHandler(_platformDb, _schemaManager, NullLogger<ActivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new ActivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_not_installed");
    }

    [Fact]
    public void ActivateModuleValidator_WithEmptyTenantId_ValidationFails()
    {
        var validator = new ActivateModuleValidator();
        var result = validator.TestValidate(new ActivateModuleCommand(Guid.Empty, "crm"));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void ActivateModuleValidator_WithEmptyModuleName_ValidationFails()
    {
        var validator = new ActivateModuleValidator();
        var result = validator.TestValidate(new ActivateModuleCommand(Guid.NewGuid(), ""));
        result.ShouldHaveValidationErrorFor(x => x.ModuleName);
    }

    public void Dispose() => _platformDb.Dispose();
}
