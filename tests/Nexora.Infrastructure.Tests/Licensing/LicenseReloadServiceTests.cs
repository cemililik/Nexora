using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.Licensing;
using Nexora.SharedKernel.Abstractions.Licensing;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Licensing;

/// <summary>
/// T-014 / ADR-0030 hot-reload watcher tests. Drives one tick at a time
/// via the public <c>PerformReloadAttemptAsync</c> hook so the polling
/// loop is not exercised in tests (deterministic, no time travel needed).
/// File replacement is the AC scenario — write file, drive tick, assert.
/// </summary>
public sealed class LicenseReloadServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _licensePath;
    private readonly InMemoryLicenseProvider _provider = new();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();

    public LicenseReloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nexora-license-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _licensePath = Path.Combine(_tempDir, "license.lic");
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_ValidFileNotPresent_NoOp()
    {
        var (svc, _) = CreateService();

        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        _provider.Current.Should().BeNull();
        await _eventBus.DidNotReceiveWithAnyArgs().PublishAsync<IIntegrationEvent>(default!, default);
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_NewValidFile_PublishesRefreshed_AndUpdatesProvider()
    {
        WriteLicense("lic-1", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();

        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        _provider.Current.Should().NotBeNull();
        _provider.Current!.LicenseId.Should().Be("lic-1");
        _provider.Current.Tier.Should().Be("professional");

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<LicenseRefreshedIntegrationEvent>(e => e.LicenseId == "lic-1" && e.Tier == "professional"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_FileReplaced_PicksUpNewLicense()
    {
        // 1) Initial valid file.
        WriteLicense("lic-old", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        _provider.Current!.LicenseId.Should().Be("lic-old");

        // 2) Replace the file (simulates admin uploading a new .lic).
        WriteLicense("lic-new", "enterprise", validUntil: DateTime.UtcNow.AddYears(2));
        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        _provider.Current!.LicenseId.Should().Be("lic-new");
        _provider.Current.Tier.Should().Be("enterprise");

        // Two refreshed events (one per swap).
        await _eventBus.Received(2).PublishAsync(
            Arg.Any<LicenseRefreshedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_UnchangedFile_IsNoOp()
    {
        WriteLicense("lic-1", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();

        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        // Only the first tick swaps + publishes — same hash short-circuits later ticks.
        await _eventBus.Received(1).PublishAsync(
            Arg.Any<LicenseRefreshedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_InvalidFile_RetainsPreviousSnapshot_AndPublishesFailed()
    {
        // 1) Seed with valid file.
        WriteLicense("lic-good", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        var goodSnapshot = _provider.Current;
        goodSnapshot.Should().NotBeNull();

        // 2) Corrupt the file with garbage (parser fails).
        File.WriteAllText(_licensePath, "{ this is not valid json --");
        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        // Provider still serves the old snapshot — rollback per AC.
        _provider.Current.Should().BeSameAs(goodSnapshot);

        // Failed event fires so admin alert routes through.
        await _eventBus.Received(1).PublishAsync(
            Arg.Any<LicenseRefreshFailedIntegrationEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_ExpiredFile_RetainsPreviousSnapshot()
    {
        WriteLicense("lic-good", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        var goodSnapshot = _provider.Current!;

        // Expired (validUntil in the past). JsonLicenseValidator returns Invalid.
        WriteLicense("lic-expired", "professional", validUntil: DateTime.UtcNow.AddDays(-1));
        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        _provider.Current.Should().BeSameAs(goodSnapshot);
        await _eventBus.Received(1).PublishAsync(
            Arg.Is<LicenseRefreshFailedIntegrationEvent>(e =>
                e.ErrorLocalizationKey == "lockey_licensing_validation_expired"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PerformReloadAttemptAsync_BadFollowedByGood_LoadsNewGoodFile()
    {
        WriteLicense("lic-good-1", "professional", validUntil: DateTime.UtcNow.AddYears(1));
        var (svc, _) = CreateService();
        await svc.PerformReloadAttemptAsync(CancellationToken.None);

        // bad → still serves previous; good → swaps.
        File.WriteAllText(_licensePath, "garbage");
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        _provider.Current!.LicenseId.Should().Be("lic-good-1");

        WriteLicense("lic-good-2", "enterprise", validUntil: DateTime.UtcNow.AddYears(2));
        await svc.PerformReloadAttemptAsync(CancellationToken.None);
        _provider.Current!.LicenseId.Should().Be("lic-good-2");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    // --- helpers ---------------------------------------------------------

    private void WriteLicense(string licenseId, string tier, DateTime validUntil)
    {
        var file = new JsonLicenseValidator.JsonLicenseFile
        {
            LicenseId = licenseId,
            Tier = tier,
            ValidFromUtc = DateTime.UtcNow.AddDays(-1),
            ValidUntilUtc = validUntil,
            Modules = ["crm", "documents"],
        };
        File.WriteAllText(_licensePath, JsonSerializer.Serialize(file));
    }

    private (LicenseReloadService Service, JsonLicenseValidator Validator) CreateService()
    {
        var opts = Options.Create(new LicenseReloadOptions
        {
            LicenseFilePath = _licensePath,
            PollingIntervalSeconds = 30,
        });
        var validator = new JsonLicenseValidator(NullLogger<JsonLicenseValidator>.Instance);
        var svc = new LicenseReloadService(
            opts, _provider, validator, _eventBus,
            NullLogger<LicenseReloadService>.Instance);
        return (svc, validator);
    }
}
