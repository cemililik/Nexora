using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.Services;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class AuditConfigServiceTests : IDisposable
{
    private readonly AuditDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public AuditConfigServiceTests()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessor = CreateTenantAccessor(_tenantId);
        _dbContext = new AuditDbContext(options, tenantAccessor);

        // Configure cache to always call through to the factory (no actual caching in tests)
        _cacheService = Substitute.For<ICacheService>();
        _cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<string>>>(),
                Arg.Any<CacheOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<string>>>(1);
                return factory(CancellationToken.None);
            });
    }

    [Fact]
    public async Task IsEnabledAsync_OperationLevelSetting_ShouldReturnOperationSetting()
    {
        // Seed operation-level setting (enabled)
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_OperationLevelDisabled_ShouldReturnFalse()
    {
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_NoOperationSetting_ShouldFallBackToModuleLevel()
    {
        // Only module-level setting (operation = "*")
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "*", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_OperationOverridesModule_ShouldPreferOperation()
    {
        // Module-level disabled
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "*", false, 90));
        // Operation-level enabled
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NoOperationOrModuleSetting_ShouldFallBackToGlobal()
    {
        // Only global setting (module = "*", operation = "*")
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "*", "*", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ModuleOverridesGlobal_ShouldPreferModule()
    {
        // Global enabled
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "*", "*", true, 90));
        // Module-level disabled
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "*", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_NoSettings_DefaultEnabledTrue_ShouldReturnTrue()
    {
        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None, defaultEnabled: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NoSettings_DefaultEnabledFalse_ShouldReturnFalse()
    {
        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None, defaultEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_TenantIsolation_ShouldNotUseOtherTenantSettings()
    {
        // Setting for a different tenant
        _dbContext.AuditSettings.Add(
            AuditSetting.Create("other-tenant", "Contacts", "CreateContact", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // Should fall back to default (true) since no setting exists for our tenant
        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_FullHierarchy_OperationShouldWin()
    {
        // Global: disabled
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "*", "*", false, 90));
        // Module: enabled
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "*", true, 90));
        // Operation: disabled
        _dbContext.AuditSettings.Add(AuditSetting.Create(_tenantId, "Contacts", "CreateContact", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DifferentOperationSamModule_ShouldFallToModule()
    {
        // Operation-level only for CreateContact
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "CreateContact", true, 90));
        // Module-level disabled
        _dbContext.AuditSettings.Add(
            AuditSetting.Create(_tenantId, "Contacts", "*", false, 90));
        await _dbContext.SaveChangesAsync();

        var service = CreateService();

        // DeleteContact has no operation-level setting, should fall back to module-level
        var result = await service.IsEnabledAsync("Contacts", "DeleteContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();

    private AuditConfigService CreateService()
    {
        var tenantAccessor = CreateTenantAccessor(_tenantId);
        return new AuditConfigService(_dbContext, _cacheService, tenantAccessor);
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
