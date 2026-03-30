using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Modules.Audit.Application.Services;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Modules.Audit.Tests.Application;

public sealed class AuditConfigServiceTests
{
    private readonly IAuditSettingRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly string _tenantId = Guid.NewGuid().ToString();

    public AuditConfigServiceTests()
    {
        _repository = Substitute.For<IAuditSettingRepository>();

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
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "contacts", "createcontact", true, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_OperationLevelDisabled_ShouldReturnFalse()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "contacts", "createcontact", false, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_NoOperationSetting_ShouldFallBackToModuleLevel()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "contacts", "*", false, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_OperationOverridesModule_ShouldPreferOperation()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "contacts", "*", false, 90),
            AuditSetting.Create(_tenantId, "contacts", "createcontact", true, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NoOperationOrModuleSetting_ShouldFallBackToGlobal()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "*", "*", false, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_ModuleOverridesGlobal_ShouldPreferModule()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "*", "*", true, 90),
            AuditSetting.Create(_tenantId, "contacts", "*", false, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_NoSettings_DefaultEnabledTrue_ShouldReturnTrue()
    {
        SetupConfigSettings("contacts", "createcontact");

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None, defaultEnabled: true);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NoSettings_DefaultEnabledFalse_ShouldReturnFalse()
    {
        SetupConfigSettings("contacts", "createcontact");

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None, defaultEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_TenantIsolation_ShouldNotUseOtherTenantSettings()
    {
        // No settings returned for our tenant (the other-tenant setting is not visible)
        SetupConfigSettings("contacts", "createcontact");

        var service = CreateService();

        // Should fall back to default (true) since no setting exists for our tenant
        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_FullHierarchy_OperationShouldWin()
    {
        SetupConfigSettings("contacts", "createcontact",
            AuditSetting.Create(_tenantId, "*", "*", false, 90),
            AuditSetting.Create(_tenantId, "contacts", "*", true, 90),
            AuditSetting.Create(_tenantId, "contacts", "createcontact", false, 90));

        var service = CreateService();

        var result = await service.IsEnabledAsync("Contacts", "CreateContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEnabledAsync_DifferentOperationSameModule_ShouldFallToModule()
    {
        // For DeleteContact query, only module-level and global would be returned
        SetupConfigSettings("contacts", "deletecontact",
            AuditSetting.Create(_tenantId, "contacts", "*", false, 90));

        var service = CreateService();

        // DeleteContact has no operation-level setting, should fall back to module-level
        var result = await service.IsEnabledAsync("Contacts", "DeleteContact", CancellationToken.None);

        result.Should().BeFalse();
    }

    private void SetupConfigSettings(string module, string operation, params AuditSetting[] settings)
    {
        _repository.FindConfigSettingsAsync(_tenantId, module, operation, Arg.Any<CancellationToken>())
            .Returns(settings as IReadOnlyList<AuditSetting>);
    }

    private AuditConfigService CreateService()
    {
        var tenantAccessor = CreateTenantAccessor(_tenantId);
        return new AuditConfigService(_repository, _cacheService, tenantAccessor,
            NullLogger<AuditConfigService>.Instance);
    }

    private static ITenantContextAccessor CreateTenantAccessor(string tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(tenantId);
        return accessor;
    }
}
