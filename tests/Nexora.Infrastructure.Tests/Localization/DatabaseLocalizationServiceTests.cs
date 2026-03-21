using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Localization;
using Nexora.Infrastructure.Localization.Entities;
using Nexora.SharedKernel.Abstractions.Caching;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Localization;

public sealed class DatabaseLocalizationServiceTests : IDisposable
{
    private readonly LocalizationDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly DatabaseLocalizationService _sut;

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    public DatabaseLocalizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LocalizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LocalizationDbContext(options);
        _cacheService = CreatePassThroughCacheService();
        _sut = new DatabaseLocalizationService(
            _dbContext, _cacheService, NullLogger<DatabaseLocalizationService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ────── GetAsync ──────

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsValue()
    {
        await SeedResource("en", "lockey_common_hello", "Hello", "common");

        var result = await _sut.GetAsync("lockey_common_hello", "en");

        result.Should().Be("Hello");
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        var result = await _sut.GetAsync("lockey_missing_key", "en");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_CaseInsensitiveLanguageCode_ReturnsValue()
    {
        await SeedResource("en", "lockey_common_ok", "OK", "common");

        var result = await _sut.GetAsync("lockey_common_ok", "EN");

        result.Should().Be("OK");
    }

    [Fact]
    public async Task GetAsync_TenantOverrideExists_ReturnsOverride()
    {
        await SeedResource("en", "lockey_common_welcome", "Welcome", "common");
        await SeedOverride(TenantA, "en", "lockey_common_welcome", "Welcome to Acme!");

        var result = await _sut.GetAsync("lockey_common_welcome", "en", TenantA);

        result.Should().Be("Welcome to Acme!");
    }

    [Fact]
    public async Task GetAsync_DifferentTenantOverride_ReturnsBaseValue()
    {
        await SeedResource("en", "lockey_common_welcome", "Welcome", "common");
        await SeedOverride(TenantA, "en", "lockey_common_welcome", "Welcome to Acme!");

        var result = await _sut.GetAsync("lockey_common_welcome", "en", TenantB);

        result.Should().Be("Welcome");
    }

    [Fact]
    public async Task GetAsync_NoTenantId_IgnoresOverrides()
    {
        await SeedResource("en", "lockey_common_welcome", "Welcome", "common");
        await SeedOverride(TenantA, "en", "lockey_common_welcome", "Welcome to Acme!");

        var result = await _sut.GetAsync("lockey_common_welcome", "en");

        result.Should().Be("Welcome");
    }

    // ────── GetManyAsync ──────

    [Fact]
    public async Task GetManyAsync_MultipleKeys_ReturnsAll()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");
        await SeedResource("en", "lockey_common_no", "No", "common");

        var result = await _sut.GetManyAsync(
            ["lockey_common_yes", "lockey_common_no"], "en");

        result.Should().HaveCount(2);
        result["lockey_common_yes"].Should().Be("Yes");
        result["lockey_common_no"].Should().Be("No");
    }

    [Fact]
    public async Task GetManyAsync_MissingKeys_OmittedFromResult()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");

        var result = await _sut.GetManyAsync(
            ["lockey_common_yes", "lockey_missing"], "en");

        result.Should().HaveCount(1);
        result.Should().ContainKey("lockey_common_yes");
        result.Should().NotContainKey("lockey_missing");
    }

    [Fact]
    public async Task GetManyAsync_WithTenantOverride_MergesCorrectly()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");
        await SeedResource("en", "lockey_common_no", "No", "common");
        await SeedOverride(TenantA, "en", "lockey_common_yes", "Yep!");

        var result = await _sut.GetManyAsync(
            ["lockey_common_yes", "lockey_common_no"], "en", TenantA);

        result["lockey_common_yes"].Should().Be("Yep!");
        result["lockey_common_no"].Should().Be("No");
    }

    [Fact]
    public async Task GetManyAsync_EmptyKeys_ReturnsEmpty()
    {
        var result = await _sut.GetManyAsync([], "en");

        result.Should().BeEmpty();
    }

    // ────── GetByModuleAsync ──────

    [Fact]
    public async Task GetByModuleAsync_FiltersCorrectly()
    {
        await SeedResource("en", "lockey_notifications_sent", "Sent", "notifications");
        await SeedResource("en", "lockey_contacts_created", "Created", "contacts");

        var result = await _sut.GetByModuleAsync("notifications", "en");

        result.Should().HaveCount(1);
        result.Should().ContainKey("lockey_notifications_sent");
        result.Should().NotContainKey("lockey_contacts_created");
    }

    [Fact]
    public async Task GetByModuleAsync_WithTenantOverride_MergesCorrectly()
    {
        await SeedResource("en", "lockey_notifications_sent", "Sent", "notifications");
        await SeedOverride(TenantA, "en", "lockey_notifications_sent", "Message Sent!");

        var result = await _sut.GetByModuleAsync("notifications", "en", TenantA);

        result["lockey_notifications_sent"].Should().Be("Message Sent!");
    }

    // ────── GetAllAsync ──────

    [Fact]
    public async Task GetAllAsync_ReturnsAllKeysForLanguage()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");
        await SeedResource("en", "lockey_notifications_sent", "Sent", "notifications");
        await SeedResource("tr", "lockey_common_yes", "Evet", "common");

        var result = await _sut.GetAllAsync("en");

        result.Should().HaveCount(2);
        result.Should().ContainKey("lockey_common_yes");
        result.Should().ContainKey("lockey_notifications_sent");
    }

    [Fact]
    public async Task GetAllAsync_WithTenantOverride_MergesCorrectly()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");
        await SeedResource("en", "lockey_common_no", "No", "common");
        await SeedOverride(TenantA, "en", "lockey_common_yes", "Absolutely!");

        var result = await _sut.GetAllAsync("en", TenantA);

        result["lockey_common_yes"].Should().Be("Absolutely!");
        result["lockey_common_no"].Should().Be("No");
    }

    [Fact]
    public async Task GetAllAsync_NonExistentLanguage_ReturnsEmpty()
    {
        await SeedResource("en", "lockey_common_yes", "Yes", "common");

        var result = await _sut.GetAllAsync("fr");

        result.Should().BeEmpty();
    }

    // ────── Helpers ──────

    private async Task SeedResource(string lang, string key, string value, string? module)
    {
        _dbContext.Resources.Add(LocalizationResource.Create(lang, key, value, module));
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedOverride(Guid tenantId, string lang, string key, string value)
    {
        _dbContext.Overrides.Add(LocalizationOverride.Create(tenantId, lang, key, value));
        await _dbContext.SaveChangesAsync();
    }

    private static ICacheService CreatePassThroughCacheService()
    {
        var cache = Substitute.For<ICacheService>();

        // Pass-through: always call the factory, never actually cache
        cache.GetOrSetAsync(
            Arg.Any<string>(),
            Arg.Any<Func<CancellationToken, Task<Dictionary<string, string>>>>(),
            Arg.Any<CacheOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var factory = callInfo.ArgAt<Func<CancellationToken, Task<Dictionary<string, string>>>>(1);
                var ct = callInfo.ArgAt<CancellationToken>(3);
                return factory(ct);
            });

        return cache;
    }
}
