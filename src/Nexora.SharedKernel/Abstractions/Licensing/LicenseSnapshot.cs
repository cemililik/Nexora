namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-014: in-memory projection of a successfully-validated license file.
/// Immutable record so the hot-reload service can swap snapshots via
/// <see cref="System.Threading.Interlocked.Exchange{T}"/> and readers
/// always see a fully-consistent view.
/// </summary>
/// <remarks>
/// All <see cref="DateTime"/> properties below normalise their input to
/// <see cref="DateTimeKind.Utc"/> on assignment via <see cref="AsUtc"/>.
/// Inputs with <see cref="DateTimeKind.Local"/> are converted via
/// <see cref="DateTime.ToUniversalTime"/>; <see cref="DateTimeKind.Unspecified"/>
/// is treated as already-UTC (the caller's responsibility — JSON bodies
/// without a timezone marker fall here). This keeps comparisons in
/// <see cref="IsActive"/> deterministic regardless of the host's timezone.
/// </remarks>
public sealed record LicenseSnapshot
{
    /// <summary>Globally-unique license identifier — matches the revocation list key.</summary>
    public required string LicenseId { get; init; }

    /// <summary>Tier name (e.g. <c>"professional"</c>, <c>"enterprise"</c>) — used for module gating.</summary>
    public required string Tier { get; init; }

    private readonly DateTime _validFromUtc;
    /// <summary>UTC timestamp the license becomes valid. Setter normalises to <see cref="DateTimeKind.Utc"/>.</summary>
    public required DateTime ValidFromUtc
    {
        get => _validFromUtc;
        init => _validFromUtc = AsUtc(value);
    }

    private readonly DateTime _validUntilUtc;
    /// <summary>UTC timestamp the license expires. Past-expiry licenses fail validation.</summary>
    public required DateTime ValidUntilUtc
    {
        get => _validUntilUtc;
        init => _validUntilUtc = AsUtc(value);
    }

    /// <summary>Module slugs the license entitles. Empty list means platform-only.</summary>
    public required IReadOnlyList<string> Modules { get; init; }

    private readonly DateTime _loadedAtUtc;
    /// <summary>UTC timestamp the snapshot was loaded from disk — operators read this for freshness diagnostics.</summary>
    public required DateTime LoadedAtUtc
    {
        get => _loadedAtUtc;
        init => _loadedAtUtc = AsUtc(value);
    }

    /// <summary>
    /// True when <paramref name="utcNow"/> is within
    /// <c>[ValidFromUtc, ValidUntilUtc]</c> — inclusive on both ends.
    /// The upper bound is intentionally inclusive: a license whose
    /// <c>ValidUntilUtc</c> equals the current instant is considered
    /// active for that single tick (the next tick fails). Caller is
    /// expected to pass a UTC instant; non-UTC <paramref name="utcNow"/>
    /// is treated as UTC for the comparison (mirrors the AsUtc rule on
    /// the stored bounds).
    /// </summary>
    public bool IsActive(DateTime utcNow)
    {
        var nowUtc = AsUtc(utcNow);
        return nowUtc >= _validFromUtc && nowUtc <= _validUntilUtc;
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Unspecified: caller asserted UTC by stuffing it into a "*Utc"
        // property; respect that assertion by tagging the Kind.
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
