namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-015: read-side of the revocation cache. License-verification call
/// sites consult this synchronously to short-circuit revoked licenses
/// before touching downstream auth logic. The implementation reads from
/// an in-memory snapshot kept fresh by the daily fetch job
/// (<c>RevocationListFetchJob</c>) — synchronous because it must be
/// callable from hot paths (gateway middleware, per-request guards).
/// </summary>
public interface IRevocationListProvider
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="licenseId"/> is
    /// in the current revocation snapshot. <see langword="false"/> when the
    /// snapshot has not yet loaded (fail-open during boot — the fetch job
    /// will catch up before requests start arriving in practice).
    /// </summary>
    bool IsRevoked(string licenseId);

    /// <summary>
    /// UTC issued-at timestamp of the snapshot currently in memory, or
    /// <see langword="null"/> if no snapshot has been loaded yet.
    /// Operators read this through diagnostics to confirm freshness.
    /// </summary>
    DateTime? CurrentBundleIssuedAtUtc { get; }
}
