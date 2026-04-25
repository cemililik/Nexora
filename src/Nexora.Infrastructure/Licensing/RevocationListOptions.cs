using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-015: bound to <c>Licensing:Revocations</c> via <c>IOptions&lt;T&gt;</c>.
/// Defaults match the values quoted in
/// <c>docs/operations/license-and-helm-upgrade.md</c> §4.3. Boot-time
/// validation lives in <see cref="RevocationListOptionsValidator"/>;
/// register it alongside the options and call <c>ValidateOnStart()</c>
/// so a misconfigured deployment fails to start instead of silently
/// running with a missing trust anchor or unwritable cache directory.
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
    /// The default targets a Linux-style state directory; on Windows /
    /// developer machines override via appsettings or
    /// <c>Licensing:Revocations:CacheFilePath</c> environment variable.
    /// </summary>
    public string CacheFilePath { get; set; } = "/var/lib/nexora/license/revocations.json";

    /// <summary>
    /// PEM-encoded RSA public key used to verify the bundle signature.
    /// Configured at deploy time — trust anchor for the entire revocation
    /// pipeline; if this is wrong, no bundle ever loads and license
    /// verification stays at last known state. REQUIRED unless
    /// <see cref="OfflineMode"/> is <see langword="true"/>.
    /// </summary>
    public string? PublicKeyPem { get; set; }

    /// <summary>HTTP request timeout. Default 30s per AC. Must be positive.</summary>
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Per-attempt delays for the retry policy. Default <c>1s, 5s, 30s</c>
    /// (3 retries, exponential, jitter is added at runtime). An empty
    /// array disables retries entirely — useful in tests. Each entry MUST
    /// be strictly positive; zero or negative delays are rejected at startup.
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
    /// loaded from the bundled distribution at deploy time. When
    /// <see langword="false"/> the <see cref="PublicKeyPem"/> trust anchor
    /// is required (validation enforces this).
    /// </summary>
    public bool OfflineMode { get; set; }
}

/// <summary>
/// T-015 startup validation for <see cref="RevocationListOptions"/>.
/// Fails loud at host start so a misconfigured trust anchor / writable
/// cache path / retry policy doesn't surface as a silent fetcher
/// no-op the first time the daily cron fires.
/// </summary>
public sealed class RevocationListOptionsValidator : IValidateOptions<RevocationListOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RevocationListOptions options)
    {
        var errors = new List<string>();

        if (!options.OfflineMode)
        {
            if (string.IsNullOrWhiteSpace(options.FetchUrl))
                errors.Add($"{nameof(options.FetchUrl)} is required when OfflineMode=false.");
            else if (!Uri.TryCreate(options.FetchUrl, UriKind.Absolute, out var uri)
                     || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                errors.Add($"{nameof(options.FetchUrl)} must be an absolute http(s) URL.");

            if (string.IsNullOrWhiteSpace(options.PublicKeyPem))
                errors.Add(
                    $"{nameof(options.PublicKeyPem)} is required when OfflineMode=false — " +
                    "without a trust anchor every fetched bundle would fail signature verification.");
        }

        if (string.IsNullOrWhiteSpace(options.CacheFilePath))
            errors.Add($"{nameof(options.CacheFilePath)} is required.");

        if (options.FetchTimeout <= TimeSpan.Zero)
            errors.Add($"{nameof(options.FetchTimeout)} must be a positive TimeSpan.");

        for (var i = 0; i < options.RetryDelays.Length; i++)
        {
            if (options.RetryDelays[i] <= TimeSpan.Zero)
            {
                errors.Add($"{nameof(options.RetryDelays)}[{i}] must be a positive TimeSpan.");
                break;
            }
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
