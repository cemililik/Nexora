using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.Licensing;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Licensing;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Licensing;

/// <summary>
/// T-015 fetch-job tests. Covers the full pipeline: HTTP fetch + signature
/// verification + atomic file persist + provider snapshot reload. Uses a
/// stub <see cref="HttpMessageHandler"/> so no real network call is made.
/// </summary>
public sealed class RevocationListFetchJobTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cachePath;
    private readonly string _privateKeyPem;
    private readonly string _publicKeyPem;

    public RevocationListFetchJobTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nexora-revocations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _cachePath = Path.Combine(_tempDir, "revocations.json");

        using var rsa = RSA.Create(2048);
        _privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        _publicKeyPem = rsa.ExportRSAPublicKeyPem();
    }

    [Fact]
    public async Task ExecuteAsync_ValidBundle_PersistsToDisk_AndUpdatesProvider()
    {
        var bundle = SignedBundle(
            new RevokedLicenseEntry("lic-001", new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc), "expired"));
        var (job, provider) = CreateJob(bundle);

        await job.RunAsync(new RevocationListFetchJobParams { TenantId = "platform" }, CancellationToken.None);

        File.Exists(_cachePath).Should().BeTrue();
        provider.IsRevoked("lic-001").Should().BeTrue();
        provider.IsRevoked("lic-other").Should().BeFalse();
        provider.CurrentBundleIssuedAtUtc.Should().Be(bundle.IssuedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_TamperedBundle_RetainsExistingCache()
    {
        // Seed cache with a previously-good bundle.
        var goodBundle = SignedBundle(new RevokedLicenseEntry("lic-good", DateTime.UtcNow, "x"));
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(goodBundle));

        // Server now returns a tampered bundle (sig still original but
        // entries mutated post-signing).
        var tampered = goodBundle with
        {
            Entries = goodBundle.Entries.Concat(
                [new RevokedLicenseEntry("lic-forged", DateTime.UtcNow, "forged")]).ToArray(),
        };
        var (job, provider) = CreateJob(tampered);

        await job.RunAsync(new RevocationListFetchJobParams { TenantId = "platform" }, CancellationToken.None);

        // Cache file unchanged — still the original bundle's contents.
        var onDisk = JsonSerializer.Deserialize<RevocationListBundle>(File.ReadAllText(_cachePath))!;
        onDisk.Entries.Should().HaveCount(1);
        onDisk.Entries[0].LicenseId.Should().Be("lic-good");

        // The forged entry never enters the in-memory snapshot.
        provider.IsRevoked("lic-forged").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_UnchangedBundle_IsNoOp()
    {
        // Round-trip: pre-load the cache + reload provider, then run the
        // job with the same bundle issued-at — expect provider's snapshot
        // unchanged (ReloadFromDiskAsync's idempotent path).
        var bundle = SignedBundle(new RevokedLicenseEntry("lic-001", DateTime.UtcNow, "x"));
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(bundle));

        var (job, provider) = CreateJob(bundle);

        // Initial reload to seed the provider.
        await provider.ReloadFromDiskAsync();
        var firstSnapshotIssuedAt = provider.CurrentBundleIssuedAtUtc;

        // Job runs with identical bundle — issuedAt stays equal, no change.
        await job.RunAsync(new RevocationListFetchJobParams { TenantId = "platform" }, CancellationToken.None);

        provider.CurrentBundleIssuedAtUtc.Should().Be(firstSnapshotIssuedAt);
    }

    [Fact]
    public async Task ExecuteAsync_OfflineMode_SkipsHttpAndReloadsCache()
    {
        // In offline mode the job MUST NOT call the HTTP endpoint.
        // We assert this by routing the HTTP handler to throw — if the
        // job calls it, the test fails loudly.
        var bundle = SignedBundle(new RevokedLicenseEntry("lic-001", DateTime.UtcNow, "x"));
        File.WriteAllText(_cachePath, JsonSerializer.Serialize(bundle));

        var (job, provider) = CreateJob(
            stubResponse: null, // intentional — handler should never run
            offline: true,
            stubThrowsIfCalled: true);

        await job.RunAsync(new RevocationListFetchJobParams { TenantId = "platform" }, CancellationToken.None);

        // Provider loaded from the bundled file.
        provider.IsRevoked("lic-001").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_NoPublicKey_AbortsWithoutTouchingCache()
    {
        var bundle = SignedBundle(new RevokedLicenseEntry("lic-001", DateTime.UtcNow, "x"));
        var (job, provider) = CreateJob(bundle, withPublicKey: false);

        await job.RunAsync(new RevocationListFetchJobParams { TenantId = "platform" }, CancellationToken.None);

        File.Exists(_cachePath).Should().BeFalse();
        provider.CurrentBundleIssuedAtUtc.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    // --- helpers ---------------------------------------------------------

    private RevocationListBundle SignedBundle(params RevokedLicenseEntry[] entries)
    {
        var bundle = new RevocationListBundle
        {
            Version = 1,
            IssuedAtUtc = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc),
            Entries = entries,
            Signature = "<placeholder>",
        };
        var canonical = RevocationListVerifier.ComputeCanonicalBytes(bundle);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKeyPem);
        var sig = rsa.SignData(canonical, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return bundle with { Signature = Convert.ToBase64String(sig) };
    }

    private (RevocationListFetchJob Job, FileRevocationListProvider Provider) CreateJob(
        RevocationListBundle? stubResponse,
        bool offline = false,
        bool withPublicKey = true,
        bool stubThrowsIfCalled = false)
    {
        var opts = new RevocationListOptions
        {
            FetchUrl = "https://test.invalid/revocations.json",
            CacheFilePath = _cachePath,
            PublicKeyPem = withPublicKey ? _publicKeyPem : null,
            FetchTimeout = TimeSpan.FromSeconds(5),
            // No retries in tests — we want fast assertions.
            RetryDelays = [],
            OfflineMode = offline,
        };
        var optionsWrapper = Options.Create(opts);

        var handler = new StubHttpMessageHandler(stubResponse, stubThrowsIfCalled);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(RevocationListFetchJob.HttpClientName)
            .Returns(_ => new HttpClient(handler) { BaseAddress = null });

        var provider = new FileRevocationListProvider(optionsWrapper, NullLogger<FileRevocationListProvider>.Instance);
        var accessor = new TenantContextAccessor();
        accessor.SetTenant(Guid.NewGuid().ToString());

        var job = new RevocationListFetchJob(
            accessor, httpClientFactory, optionsWrapper, provider,
            NullLogger<RevocationListFetchJob>.Instance);
        return (job, provider);
    }

    private sealed class StubHttpMessageHandler(RevocationListBundle? response, bool throwIfCalled) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (throwIfCalled)
                throw new InvalidOperationException("HTTP handler must not be invoked in offline mode.");

            if (response is null)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

            var json = JsonSerializer.Serialize(response);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
