namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-014: in-memory projection of a successfully-validated license file.
/// Immutable record so the hot-reload service can swap snapshots via
/// <see cref="System.Threading.Interlocked.Exchange{T}"/> and readers
/// always see a fully-consistent view.
/// </summary>
public sealed record LicenseSnapshot
{
    /// <summary>Globally-unique license identifier — matches the revocation list key.</summary>
    public required string LicenseId { get; init; }

    /// <summary>Tier name (e.g. <c>"professional"</c>, <c>"enterprise"</c>) — used for module gating.</summary>
    public required string Tier { get; init; }

    /// <summary>UTC timestamp the license becomes valid.</summary>
    public required DateTime ValidFromUtc { get; init; }

    /// <summary>UTC timestamp the license expires. Past-expiry licenses fail validation.</summary>
    public required DateTime ValidUntilUtc { get; init; }

    /// <summary>Module slugs the license entitles. Empty list means platform-only.</summary>
    public required IReadOnlyList<string> Modules { get; init; }

    /// <summary>UTC timestamp the snapshot was loaded from disk — operators read this for freshness diagnostics.</summary>
    public required DateTime LoadedAtUtc { get; init; }

    /// <summary>True when <c>now</c> is within <c>[ValidFromUtc, ValidUntilUtc]</c>.</summary>
    public bool IsActive(DateTime utcNow) => utcNow >= ValidFromUtc && utcNow <= ValidUntilUtc;
}
