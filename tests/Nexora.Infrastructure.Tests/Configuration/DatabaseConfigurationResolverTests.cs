using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Configuration;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Configuration;

/// <summary>
/// Exercises <see cref="DatabaseConfigurationResolver"/> precedence rules per ADR-0025:
/// <c>cap.forced &gt; org &gt; tenant &gt; cap.default</c>.
/// Uses EF InMemory + a bypass cache so the precedence logic is isolated.
/// </summary>
public sealed class DatabaseConfigurationResolverTests : IDisposable
{
    private const string Key = "gdpr.hard_delete.enabled";

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly TenantConfigDbContext _dbContext;
    private readonly IComplianceCapProvider _capProvider = Substitute.For<IComplianceCapProvider>();
    private readonly ICacheService _cache = new BypassCacheService();
    private readonly ITenantContextAccessor _tenantAccessor;

    public DatabaseConfigurationResolverTests()
    {
        _tenantAccessor = new TenantContextAccessor();
        _tenantAccessor.SetTenant(_tenantId.ToString(), _orgId.ToString(), _userId.ToString());

        var options = new DbContextOptionsBuilder<TenantConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TenantConfigDbContext(options, _tenantAccessor);

        _capProvider.GetCapAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ComplianceCap.Permissive);
    }

    private DatabaseConfigurationResolver CreateResolver() =>
        new(_dbContext, _capProvider, _tenantAccessor, _cache, NullLogger<DatabaseConfigurationResolver>.Instance);

    [Fact]
    public async Task Get_NoLayerSet_ReturnsDefault()
    {
        var resolver = CreateResolver();

        var value = await resolver.GetAsync<bool>(Key);

        value.Should().BeFalse(); // default(bool)
    }

    [Fact]
    public async Task Get_TenantDefaultOnly_ReturnsTenantDefault()
    {
        _dbContext.Configurations.Add(new TenantConfigEntry
        {
            Key = Key,
            Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var resolver = CreateResolver();
        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeTrue();
        resolved.WinningLayer.Should().Be(ResolutionLayer.TenantDefault);
        resolved.OrgOverride.Should().BeFalse(); // default(bool) when none
    }

    [Fact]
    public async Task Get_OrgOverrideBeatsTenantDefault()
    {
        _dbContext.Configurations.Add(new TenantConfigEntry
        {
            Key = Key, Value = "false", UpdatedAt = DateTimeOffset.UtcNow
        });
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = _orgId, Key = Key, Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = _userId.ToString()
        });
        await _dbContext.SaveChangesAsync();

        var resolver = CreateResolver();
        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeTrue();
        resolved.WinningLayer.Should().Be(ResolutionLayer.OrgOverride);
    }

    [Fact]
    public async Task Get_ForcedCapBeatsAllLowerLayers()
    {
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = _orgId, Key = Key, Value = "false",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = _userId.ToString()
        });
        await _dbContext.SaveChangesAsync();

        _capProvider.GetCapAsync(Key, Arg.Any<CancellationToken>())
            .Returns(new ComplianceCap(Allowed: true, Forced: true, Value: "true"));

        var resolver = CreateResolver();
        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeTrue();
        resolved.WinningLayer.Should().Be(ResolutionLayer.Cap);
    }

    [Fact]
    public async Task Get_CapAllowedFalse_IgnoresLowerLayers_EvenIfPresent()
    {
        _dbContext.Configurations.Add(new TenantConfigEntry
        {
            Key = Key, Value = "true", UpdatedAt = DateTimeOffset.UtcNow
        });
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = _orgId, Key = Key, Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = _userId.ToString()
        });
        await _dbContext.SaveChangesAsync();

        // Cap tightened to disallow — pre-existing org/tenant values must not leak through.
        _capProvider.GetCapAsync(Key, Arg.Any<CancellationToken>())
            .Returns(new ComplianceCap(Allowed: false, Forced: false, Value: "false"));

        var resolver = CreateResolver();
        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeFalse();
        resolved.WinningLayer.Should().Be(ResolutionLayer.Cap);
    }

    [Fact]
    public async Task SetOrgOverride_CapDisallows_AuditRecordsPriorOverrideAsOldValue()
    {
        // Pre-existing override left over from before the cap tightened.
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = _orgId, Key = Key, Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = _userId.ToString()
        });
        await _dbContext.SaveChangesAsync();

        _capProvider.GetCapAsync(Key, Arg.Any<CancellationToken>())
            .Returns(new ComplianceCap(Allowed: false, Forced: false));

        var resolver = CreateResolver();

        var act = () => resolver.SetOrgOverrideAsync(Key, false, "attempting flip");
        await act.Should().ThrowAsync<ComplianceCapViolationException>();

        var audit = await _dbContext.PolicyAudit.SingleAsync();
        audit.OldValue.Should().Be("true");
        audit.NewValue.Should().Be("false");
    }

    [Fact]
    public async Task Get_NoOrgContext_SkipsOrgLayer()
    {
        // Reset accessor to omit org id
        var tenantOnly = new TenantContextAccessor();
        tenantOnly.SetTenant(_tenantId.ToString(), organizationId: null, userId: _userId.ToString());
        var options = new DbContextOptionsBuilder<TenantConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new TenantConfigDbContext(options, tenantOnly);
        db.Configurations.Add(new TenantConfigEntry
        {
            Key = Key, Value = "true", UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var resolver = new DatabaseConfigurationResolver(
            db, _capProvider, tenantOnly, _cache, NullLogger<DatabaseConfigurationResolver>.Instance);

        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeTrue();
        resolved.WinningLayer.Should().Be(ResolutionLayer.TenantDefault);
    }

    [Fact]
    public async Task SetOrgOverride_WritesAuditRowAtomically()
    {
        var resolver = CreateResolver();

        await resolver.SetOrgOverrideAsync(Key, true, "legal requested enablement");

        var overrideRow = await _dbContext.OrgOverrides
            .SingleAsync(c => c.OrganizationId == _orgId && c.Key == Key);
        overrideRow.Value.Should().Be("true");

        var audit = await _dbContext.PolicyAudit.SingleAsync();
        audit.TenantId.Should().Be(_tenantId);
        audit.OrganizationId.Should().Be(_orgId);
        audit.Key.Should().Be(Key);
        audit.OldValue.Should().BeNull();
        audit.NewValue.Should().Be("true");
        audit.ChangedByUserId.Should().Be(_userId);
        audit.Reason.Should().Be("legal requested enablement");
    }

    [Fact]
    public async Task SetOrgOverride_CapDisallows_ThrowsViolation_AndRecordsAuditRow()
    {
        _capProvider.GetCapAsync(Key, Arg.Any<CancellationToken>())
            .Returns(new ComplianceCap(Allowed: false, Forced: false));

        var resolver = CreateResolver();

        var act = () => resolver.SetOrgOverrideAsync(Key, true, "attempting anyway");

        (await act.Should().ThrowAsync<ComplianceCapViolationException>())
            .Where(ex => ex.Key == Key && ex.Allowed == false && ex.IsForced == false);

        // Override not persisted, but audit row IS — forensic trail must capture blocks.
        (await _dbContext.OrgOverrides.AnyAsync()).Should().BeFalse();
        var audit = await _dbContext.PolicyAudit.SingleAsync();
        audit.Reason.Should().Contain(DatabaseConfigurationResolver.CapBlockedRejectedSuffix);
    }

    [Fact]
    public async Task SetOrgOverride_CapForced_ThrowsViolation_AndRecordsAttempt()
    {
        _capProvider.GetCapAsync(Key, Arg.Any<CancellationToken>())
            .Returns(new ComplianceCap(Allowed: true, Forced: true, Value: "true"));

        var resolver = CreateResolver();

        var act = () => resolver.SetOrgOverrideAsync(Key, false, "trying to disable");

        (await act.Should().ThrowAsync<ComplianceCapViolationException>())
            .Where(ex => ex.Key == Key && ex.IsForced && ex.ForcedValue == "true");

        // Override not persisted; audit row is — forensic trail captures blocked attempts.
        (await _dbContext.OrgOverrides.AnyAsync()).Should().BeFalse();
        var audit = await _dbContext.PolicyAudit.SingleAsync();
        audit.Reason.Should().Contain(DatabaseConfigurationResolver.CapForcedRejectedSuffix);
    }

    [Fact]
    public async Task ClearOrgOverride_RemovesOverride_WritesAudit()
    {
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = _orgId, Key = Key, Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = _userId.ToString()
        });
        await _dbContext.SaveChangesAsync();

        var resolver = CreateResolver();
        await resolver.ClearOrgOverrideAsync(Key, "no longer needed");

        (await _dbContext.OrgOverrides.AnyAsync()).Should().BeFalse();
        var audit = await _dbContext.PolicyAudit.SingleAsync();
        audit.OldValue.Should().Be("true");
        audit.NewValue.Should().BeNull();
        audit.Reason.Should().Be("no longer needed");
    }

    [Fact]
    public async Task ClearOrgOverride_NoExistingOverride_IsNoOp()
    {
        var resolver = CreateResolver();

        await resolver.ClearOrgOverrideAsync(Key, "cleanup");

        (await _dbContext.OrgOverrides.AnyAsync()).Should().BeFalse();
        (await _dbContext.PolicyAudit.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task OrgOverride_OneOrgDoesNotLeakToAnother()
    {
        var otherOrg = Guid.NewGuid();
        _dbContext.OrgOverrides.Add(new OrgConfigEntry
        {
            OrganizationId = otherOrg, Key = Key, Value = "true",
            UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = Guid.NewGuid().ToString()
        });
        _dbContext.Configurations.Add(new TenantConfigEntry
        {
            Key = Key, Value = "false", UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var resolver = CreateResolver();
        var resolved = await resolver.GetResolvedAsync<bool>(Key);

        resolved.Effective.Should().BeFalse();
        resolved.WinningLayer.Should().Be(ResolutionLayer.TenantDefault);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>
    /// Test double that bypasses caching: every GetOrSet calls the factory; Remove is a no-op.
    /// Keeps the resolver under test instead of the cache layer, which is exercised separately.
    /// </summary>
    private sealed class BypassCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult<T?>(default);

        public async Task<T> GetOrSetAsync<T>(
            string key, Func<CancellationToken, Task<T>> factory,
            CacheOptions? options = null, CancellationToken ct = default)
            => await factory(ct);

        public Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
