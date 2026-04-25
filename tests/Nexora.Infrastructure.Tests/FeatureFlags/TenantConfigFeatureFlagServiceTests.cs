using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Configuration;
using Nexora.Infrastructure.FeatureFlags;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.FeatureFlags;

namespace Nexora.Infrastructure.Tests.FeatureFlags;

/// <summary>
/// T-016 tests for the tenant-config-backed feature-flag service. Uses
/// EF InMemory so the round-trip exercises the full storage shape
/// (<c>feature_flags.{key}</c> prefix, JSON serialisation) without
/// requiring Postgres.
/// </summary>
public sealed class TenantConfigFeatureFlagServiceTests : IDisposable
{
    private readonly TenantConfigDbContext _dbContext;
    private readonly DatabaseTenantConfiguration _tenantConfig;
    private readonly TenantContextAccessor _accessor;

    public TenantConfigFeatureFlagServiceTests()
    {
        _accessor = new TenantContextAccessor();
        _accessor.SetTenant(Guid.NewGuid().ToString());

        var options = new DbContextOptionsBuilder<TenantConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TenantConfigDbContext(options, _accessor);

        _tenantConfig = new DatabaseTenantConfiguration(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private IFeatureFlagService CreateService() =>
        new TenantConfigFeatureFlagService(_tenantConfig, _dbContext);

    [Fact]
    public async Task IsEnabledAsync_FlagAbsent_ReturnsFalse()
    {
        var svc = CreateService();
        (await svc.IsEnabledAsync("crm.kanban_v2")).Should().BeFalse(
            "absent flags default to off — convention enforced everywhere");
    }

    [Fact]
    public async Task SetAsync_ThenIsEnabledAsync_RoundTripsTrue()
    {
        var svc = CreateService();
        await svc.SetAsync("crm.kanban_v2", enabled: true);

        (await svc.IsEnabledAsync("crm.kanban_v2")).Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_ThenSetFalse_FlagDisabled()
    {
        var svc = CreateService();
        await svc.SetAsync("docs.parallel_upload", true);
        await svc.SetAsync("docs.parallel_upload", false);

        (await svc.IsEnabledAsync("docs.parallel_upload")).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyFlagPrefixedKeys()
    {
        var svc = CreateService();
        await svc.SetAsync("crm.kanban_v2", true);
        await svc.SetAsync("docs.parallel_upload", false);

        // Direct write of a non-flag key — must NOT appear in GetAllAsync.
        await _tenantConfig.SetAsync("locale.default", "en-US");

        var all = await svc.GetAllAsync();
        all.Should().HaveCount(2);
        all.Should().ContainKey("crm.kanban_v2").WhoseValue.Should().BeTrue();
        all.Should().ContainKey("docs.parallel_upload").WhoseValue.Should().BeFalse();
        all.Should().NotContainKey("locale.default");
    }

    [Fact]
    public async Task SetAsync_StoresUnderFeatureFlagsPrefix()
    {
        var svc = CreateService();
        await svc.SetAsync("crm.kanban_v2", true);

        // The bare key MUST NOT exist on its own — only under the prefix.
        var stored = await _tenantConfig.GetAsync<bool>("crm.kanban_v2");
        stored.Should().BeFalse("the service prefixes all writes with feature_flags.");

        var prefixed = await _tenantConfig.GetAsync<bool>(TenantConfigFeatureFlagService.FlagKeyPrefix + "crm.kanban_v2");
        prefixed.Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_IgnoresUserIdInDefaultBackend()
    {
        var svc = CreateService();
        await svc.SetAsync("crm.kanban_v2", true);

        // userId is currently a pass-through param; tenant-scoped value
        // wins regardless. A future LaunchDarkly backend would hash on
        // userId — call sites stay unchanged.
        (await svc.IsEnabledAsync("crm.kanban_v2", userId: Guid.NewGuid())).Should().BeTrue();
        (await svc.IsEnabledAsync("crm.kanban_v2", userId: null)).Should().BeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_ThrowsOnEmptyKey()
    {
        var svc = CreateService();
        var act = async () => await svc.IsEnabledAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
