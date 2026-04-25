namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-015: bound to <c>Licensing:Revocations</c> via <c>IOptions&lt;T&gt;</c>.
/// All defaults match the values quoted in
/// <c>docs/operations/license-and-helm-upgrade.md</c> §4.3.
/// </summary>
public sealed class RevocationListOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "Licensing:Revocations";

    /// <summary>HTTPS endpoint serving the signed bundle. Default <c>https://license.nexora.io/revocations.json</c>.</summary>
    public string FetchUrl { get; set; } = "https://license.nexora.io/revocations.json";

    /// <summary>
    /// Local file path the fetched bundle is persisted to (atomic stage →
    /// rename). Read at startup by <c>FileRevocationListProvider</c>.
    /// Default keeps it inside the host's writable state directory.
    /// </summary>
    public string CacheFilePath { get; set; } = "/var/lib/nexora/license/revocations.json";

    /// <summary>
    /// PEM-encoded RSA public key used to verify the bundle signature.
    /// Configured at deploy time — trust anchor for the entire revocation
    /// pipeline; if this is wrong, no bundle ever loads and license
    /// verification stays at last known state.
    /// </summary>
    public string? PublicKeyPem { get; set; }

    /// <summary>HTTP request timeout. Default 30s per AC.</summary>
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Per-attempt delays for the retry policy. Default <c>1s, 5s, 30s</c>
    /// (3 retries, exponential, jitter is added at runtime). An empty
    /// array disables retries entirely — useful in tests.
    /// </summary>
    public TimeSpan[] RetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>
    /// True for air-gapped / on-prem deployments — the fetcher skips the
    /// HTTP call entirely and the file at <see cref="CacheFilePath"/> is
    /// loaded from the bundled distribution at deploy time.
    /// </summary>
    public bool OfflineMode { get; set; }
}
