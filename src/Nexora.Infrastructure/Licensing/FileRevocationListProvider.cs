using System.Collections.Frozen;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-015: keeps the verified revocation snapshot in memory so query
/// callers (gateway, license verifier) get O(1) lookups without disk
/// I/O. The fetch job (<c>RevocationListFetchJob</c>) writes the new
/// bundle to disk, then calls <see cref="ReloadFromDiskAsync"/> to
/// publish it via <see cref="System.Threading.Interlocked.Exchange{T}"/>
/// — readers always see a fully-valid snapshot, never a torn intermediate.
/// </summary>
public sealed class FileRevocationListProvider(
    IOptions<RevocationListOptions> options,
    ILogger<FileRevocationListProvider> logger) : IRevocationListProvider
{
    private readonly RevocationListOptions _opts = options.Value;
    private Snapshot? _snapshot;

    /// <inheritdoc />
    public bool IsRevoked(string licenseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseId);
        var snap = Volatile.Read(ref _snapshot);
        return snap is not null && snap.RevokedIds.Contains(licenseId);
    }

    /// <inheritdoc />
    public DateTime? CurrentBundleIssuedAtUtc => Volatile.Read(ref _snapshot)?.IssuedAtUtc;

    /// <summary>
    /// Reads the bundle from <see cref="RevocationListOptions.CacheFilePath"/>
    /// and atomically swaps it into <see cref="_snapshot"/>. Idempotent —
    /// callers may invoke this on every fetch even when nothing changed;
    /// the file's <c>IssuedAtUtc</c> is checked and a no-op is logged.
    /// Returns <see langword="true"/> when the snapshot was actually
    /// updated (new bundle), <see langword="false"/> for no-ops and
    /// errors (errors are logged inside).
    /// </summary>
    public async Task<bool> ReloadFromDiskAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_opts.CacheFilePath))
        {
            logger.LogWarning(
                "Revocation list reload: cache file {Path} does not exist; license verification continues with no revocations.",
                _opts.CacheFilePath);
            return false;
        }

        RevocationListBundle? bundle;
        try
        {
            await using var stream = File.OpenRead(_opts.CacheFilePath);
            bundle = await JsonSerializer.DeserializeAsync<RevocationListBundle>(stream, cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Revocation list reload: cache file {Path} is malformed JSON; previous snapshot retained.",
                _opts.CacheFilePath);
            return false;
        }
        catch (IOException ex)
        {
            logger.LogError(ex,
                "Revocation list reload: cache file {Path} is unreadable; previous snapshot retained.",
                _opts.CacheFilePath);
            return false;
        }

        if (bundle is null)
        {
            logger.LogError("Revocation list reload: cache file {Path} deserialized to null.", _opts.CacheFilePath);
            return false;
        }

        // Tampered bundles can ship with Entries == null even though the
        // type declares it required; guard before the LINQ call below or
        // we get an NRE that aborts the reload service.
        if (bundle.Entries is null)
        {
            logger.LogError(
                "Revocation list reload: cache file {Path} (issued {IssuedAt:O}) has null Entries; previous snapshot retained.",
                _opts.CacheFilePath, bundle.IssuedAtUtc);
            return false;
        }

        var current = Volatile.Read(ref _snapshot);
        if (current is not null && current.IssuedAtUtc == bundle.IssuedAtUtc)
        {
            logger.LogDebug(
                "Revocation list reload: bundle issued-at {IssuedAt:O} matches the in-memory snapshot — no-op.",
                bundle.IssuedAtUtc);
            return false;
        }

        var snapshot = new Snapshot(
            bundle.IssuedAtUtc,
            bundle.Entries.Select(e => e.LicenseId).ToFrozenSet());

        Interlocked.Exchange(ref _snapshot, snapshot);

        logger.LogInformation(
            "Revocation list reload: loaded {Count} revocation(s) issued {IssuedAt:O}.",
            snapshot.RevokedIds.Count, snapshot.IssuedAtUtc);
        return true;
    }

    private sealed record Snapshot(DateTime IssuedAtUtc, FrozenSet<string> RevokedIds);
}
