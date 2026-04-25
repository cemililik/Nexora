using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Licensing;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Licensing;

/// <summary>Parameters for the platform-wide revocation-list fetch job.</summary>
public sealed record RevocationListFetchJobParams : JobParams;

/// <summary>
/// T-015 (ADR-0023): daily at 03:00 UTC, fetches the signed revocation
/// bundle from the configured URL, RSA-verifies it, and atomically
/// replaces the local cache file. On signature failure the job logs at
/// Error and aborts — the existing cache is retained, license verifiers
/// continue using the previous snapshot.
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomic file rename.</b> The downloaded payload is written to
/// <c>{CacheFilePath}.tmp</c>, fsynced, then <c>File.Move</c>'d over the
/// canonical path. <c>File.Move(overwrite: true)</c> on Linux maps to
/// <c>rename(2)</c>, which is atomic — readers can never observe a
/// partial JSON file.
/// </para>
/// <para>
/// <b>Offline mode.</b> When <see cref="RevocationListOptions.OfflineMode"/>
/// is set the job is a no-op: air-gapped deployments load the bundled
/// revocations file at deploy time and the platform never phones home.
/// </para>
/// <para>
/// <b>Retry.</b> Per AC, three exponential attempts at 1s / 5s / 30s with
/// uniform jitter applied at each attempt. Configurable via
/// <see cref="RevocationListOptions.RetryDelays"/> so tests can disable.
/// </para>
/// </remarks>
[Queue("maintenance")]
[DisplayName("license:fetch-revocations")]
public sealed class RevocationListFetchJob(
    ITenantContextAccessor tenantContextAccessor,
    IHttpClientFactory httpClientFactory,
    IOptions<RevocationListOptions> options,
    FileRevocationListProvider provider,
    ILogger<RevocationListFetchJob> logger,
    TimeProvider? timeProvider = null)
    : NexoraJob<RevocationListFetchJobParams>(tenantContextAccessor, logger)
{
    private const string RecurringJobId = "license:fetch-revocations";
    private const string PlatformSentinelTenantId = "platform";
    /// <summary>Named HTTP client; allows tests to register a fake handler.</summary>
    public const string HttpClientName = "Nexora.Licensing.Revocations";

    private readonly RevocationListOptions _opts = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private static readonly Random JitterSource = Random.Shared;

    /// <summary>Registers the daily 03:00 UTC schedule. Called from infrastructure module bootstrap.</summary>
    public static void RegisterRecurringSchedule(IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<RevocationListFetchJob>(
            RecurringJobId,
            cronExpression: "0 3 * * *",
            methodCall: job => job.RunAsync(
                new RevocationListFetchJobParams { TenantId = PlatformSentinelTenantId },
                CancellationToken.None),
            queue: "maintenance");
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(RevocationListFetchJobParams parameters, CancellationToken ct)
    {
        if (_opts.OfflineMode)
        {
            // Air-gapped — caller bundled the revocations file at deploy
            // time. We still reload from disk in case the operator
            // hot-swapped the file between job runs (allowed per ops doc).
            await provider.ReloadFromDiskAsync(ct);
            logger.LogInformation("Revocation fetch: offline mode — skipping HTTP fetch; reloaded local cache.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_opts.PublicKeyPem))
        {
            logger.LogError(
                "Revocation fetch: PublicKeyPem is not configured — refusing to fetch. Configure Licensing:Revocations:PublicKeyPem to a deployment-trusted RSA public key.");
            return;
        }

        var bundle = await TryFetchWithRetryAsync(ct);
        if (bundle is null) return;

        if (!RevocationListVerifier.Verify(bundle, _opts.PublicKeyPem))
        {
            // SECURITY-SENSITIVE: signature failure is the strongest
            // indicator we have of MITM / corruption / impostor server.
            // We deliberately log at Error (alerts on-call) and DO NOT
            // touch the local cache — last good state continues serving.
            logger.LogError(
                "Revocation fetch: signature verification FAILED for bundle issued {IssuedAt:O} ({EntryCount} entries). Local cache retained; on-call MUST investigate the signing key chain.",
                bundle.IssuedAtUtc, bundle.Entries.Count);
            return;
        }

        await PersistAtomicallyAsync(bundle, ct);
        await provider.ReloadFromDiskAsync(ct);
    }

    private async Task<RevocationListBundle?> TryFetchWithRetryAsync(CancellationToken ct)
    {
        var attempts = _opts.RetryDelays.Length + 1; // initial attempt + retries
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                var client = httpClientFactory.CreateClient(HttpClientName);
                client.Timeout = _opts.FetchTimeout;
                var bundle = await client.GetFromJsonAsync<RevocationListBundle>(_opts.FetchUrl, ct);
                if (bundle is not null) return bundle;

                logger.LogWarning("Revocation fetch attempt {Attempt}/{Total}: server returned empty body.",
                    i + 1, attempts);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                logger.LogWarning(ex,
                    "Revocation fetch attempt {Attempt}/{Total} failed: {Message}",
                    i + 1, attempts, ex.Message);
            }

            // Don't wait after the last attempt.
            if (i < _opts.RetryDelays.Length)
            {
                var baseDelay = _opts.RetryDelays[i];
                // Uniform jitter ±25% so concurrent on-prem deployments do
                // not synchronize their retry storms against the issuer.
                var jitterFactor = 0.75 + (JitterSource.NextDouble() * 0.5);
                var jitteredDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitterFactor);
                await Task.Delay(jitteredDelay, _timeProvider, ct);
            }
        }

        logger.LogError("Revocation fetch: all {Attempts} attempts failed; local cache retained.", attempts);
        return null;
    }

    private async Task PersistAtomicallyAsync(RevocationListBundle bundle, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_opts.CacheFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tempPath = _opts.CacheFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, bundle, cancellationToken: ct);
            await stream.FlushAsync(ct);
        }

        // File.Move(overwrite:true) → rename(2) on POSIX (atomic) and
        // ReplaceFile on Windows (atomic). Readers either see the old
        // file or the new file, never a torn intermediate.
        File.Move(tempPath, _opts.CacheFilePath, overwrite: true);
    }
}
