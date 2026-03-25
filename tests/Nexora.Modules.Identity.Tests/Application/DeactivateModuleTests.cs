using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Identity.Application.Commands;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;

namespace Nexora.Modules.Identity.Tests.Application;

public sealed class DeactivateModuleTests : IDisposable
{
    private readonly PlatformDbContext _platformDb;
    private readonly TenantId _tenantId = TenantId.New();

    public DeactivateModuleTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        _platformDb = new PlatformDbContext(options);

        // Create and seed tenant
        var tenant = Tenant.Create("Test", "test");
        typeof(Tenant).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tenant, _tenantId);
        tenant.Activate();
        _platformDb.Tenants.Add(tenant);
        _platformDb.SaveChanges();
    }

    [Fact]
    public async Task DeactivateModule_WithActiveModule_DeactivatesAndReturnsSuccess()
    {
        // Arrange
        _platformDb.TenantModules.Add(TenantModule.Create(_tenantId, "crm"));
        await _platformDb.SaveChangesAsync();

        var handler = new DeactivateModuleHandler(_platformDb, NullLogger<DeactivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeactivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var updated = await _platformDb.TenantModules
            .FirstAsync(tm => tm.TenantId == _tenantId && tm.ModuleName == "crm");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateModule_WithAlreadyInactiveModule_ReturnsFailure()
    {
        // Arrange
        var module = TenantModule.Create(_tenantId, "crm");
        module.Deactivate();
        _platformDb.TenantModules.Add(module);
        await _platformDb.SaveChangesAsync();

        var handler = new DeactivateModuleHandler(_platformDb, NullLogger<DeactivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeactivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_already_inactive");
    }

    [Fact]
    public async Task DeactivateModule_WithNotInstalledModule_ReturnsFailure()
    {
        // Arrange — no module seeded
        var handler = new DeactivateModuleHandler(_platformDb, NullLogger<DeactivateModuleHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new DeactivateModuleCommand(_tenantId.Value, "crm"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Key.Should().Be("lockey_identity_error_module_not_installed");
    }

    [Fact]
    public void DeactivateModuleValidator_WithEmptyTenantId_ValidationFails()
    {
        var validator = new DeactivateModuleValidator();
        var result = validator.TestValidate(new DeactivateModuleCommand(Guid.Empty, "crm"));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    public void Dispose() => _platformDb.Dispose();
}
