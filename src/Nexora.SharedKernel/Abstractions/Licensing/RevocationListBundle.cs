namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-015: signed revocation bundle published at
/// <c>https://license.nexora.io/revocations.json</c>. The platform fetches
/// this daily; on-prem / air-gapped deployments load it from a bundled
/// file. RSA-SHA256 signature is verified before the local cache is
/// updated — see <c>RevocationListVerifier</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Wire format.</b> The signed payload is the JSON object
/// <c>{ Version, IssuedAtUtc, Entries }</c> in canonical (alphabetical
/// property name) UTF-8 form; the <see cref="Signature"/> is computed over
/// that canonical bytes by Nexora's signing key. Verifier MUST recompute
/// the canonical form before validating — pretty-printing or whitespace
/// changes between the issuer and the verifier would otherwise break the
/// signature even though the data is identical.
/// </para>
/// <para>
/// <b>Versioning.</b> <see cref="Version"/> starts at <c>1</c>. Increments
/// indicate breaking changes to the bundle's outer shape — the verifier
/// rejects unknown versions rather than guessing.
/// </para>
/// </remarks>
public sealed record RevocationListBundle
{
    /// <summary>Outer-shape version. Always <c>1</c> at first issuance.</summary>
    public required int Version { get; init; }

    /// <summary>UTC timestamp the bundle was issued. Used to detect stale rolls.</summary>
    public required DateTime IssuedAtUtc { get; init; }

    /// <summary>The revoked-license entries — empty list is valid.</summary>
    public required IReadOnlyList<RevokedLicenseEntry> Entries { get; init; }

    /// <summary>Base64-encoded RSA-SHA256 signature over the canonical bundle bytes.</summary>
    public required string Signature { get; init; }
}

/// <summary>
/// One revoked license — append-only inside the bundle. Once an entry
/// appears in any signed bundle, it remains in every subsequent issuance
/// so offline deployments that load an older bundle don't re-allow a
/// previously-revoked license.
/// </summary>
/// <param name="LicenseId">
/// The revoked license's identifier — matches <c>LicenseSnapshot.LicenseId</c>
/// (the deployment-trusted ID stamped by the issuer at license creation).
/// Lookup key for <see cref="IRevocationListProvider.IsRevoked"/>.
/// </param>
/// <param name="RevokedAtUtc">
/// UTC timestamp the issuer marked the license as revoked. Treated as UTC
/// during canonical-bytes computation regardless of the in-memory
/// <see cref="DateTime.Kind"/>; canonicalisation calls <c>ToUniversalTime</c>
/// so an issuer that constructs the entry with <c>Kind = Unspecified</c>
/// still produces the same signed bytes.
/// </param>
/// <param name="Reason">
/// Free-text classification (e.g. <c>"expired"</c>, <c>"fraud"</c>,
/// <c>"cancelled"</c>). NOT a closed enum — the issuer is free to add new
/// reasons over time. Verifier MUST NOT branch on this string; it is
/// included in the canonical bytes only so audit / ops can triage.
/// </param>
public sealed record RevokedLicenseEntry(
    string LicenseId,
    DateTime RevokedAtUtc,
    string Reason);
