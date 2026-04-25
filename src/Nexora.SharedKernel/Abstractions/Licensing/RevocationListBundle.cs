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

/// <summary>One revoked license — append-only inside the bundle.</summary>
public sealed record RevokedLicenseEntry(
    string LicenseId,
    DateTime RevokedAtUtc,
    string Reason);
