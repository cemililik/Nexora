using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-015: verifies a <see cref="RevocationListBundle"/> against a PEM-encoded
/// RSA public key using RSA-SHA256. Side-effect-free — pure function over
/// (bundle, publicKeyPem) so unit tests can exercise tamper detection
/// without touching disk or HTTP.
/// </summary>
public static class RevocationListVerifier
{
    /// <summary>
    /// Every serializer option that could produce a different byte stream
    /// is pinned explicitly so a future .NET runtime upgrade or a global
    /// JsonSerializerOptions change cannot silently invalidate signatures
    /// on bundles that were valid before. Issuer and verifier MUST share
    /// these options.
    /// </summary>
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        PropertyNameCaseInsensitive = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.Strict,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Returns <see langword="true"/> when the bundle's signature is valid
    /// for the supplied public key. Catches all crypto / parsing
    /// exceptions and returns <see langword="false"/> — call sites cannot
    /// distinguish "tampered" from "malformed", and that is intentional:
    /// either way the bundle MUST be rejected.
    /// </summary>
    public static bool Verify(RevocationListBundle bundle, string publicKeyPem)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);

        try
        {
            var canonicalBytes = ComputeCanonicalBytes(bundle);
            var signatureBytes = Convert.FromBase64String(bundle.Signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            return rsa.VerifyData(
                canonicalBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (
            ex is FormatException
                or CryptographicException
                or ArgumentException)
        {
            // FormatException → base64 garbage in the Signature field.
            // CryptographicException → bad PEM, wrong key length / curve mismatch.
            // ArgumentException → RSA.ImportFromPem rejects malformed PEM
            //   labels with this type rather than CryptographicException.
            // Tamper / malformed: either way the bundle MUST be rejected.
            return false;
        }
    }

    /// <summary>
    /// Produces the canonical UTF-8 byte representation of the bundle
    /// (everything except the signature) for signing and verification.
    /// Public so the issuer and tests can produce byte-identical input.
    /// </summary>
    public static byte[] ComputeCanonicalBytes(RevocationListBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        // Signature is computed over a "signature-stripped" projection so
        // the issuer and verifier agree on what the signed payload is.
        // Property order is the alphabetical projection used by the issuer.
        // AsUtc normalises the DateTime.Kind: a DateTime stored with
        // Kind.Unspecified would otherwise serialise without the trailing
        // "Z" while the same instant marked Kind.Utc would include it —
        // canonical bytes diverge and signature verification fails.
        var canonical = new
        {
            entries = bundle.Entries
                .Select(e => new { e.LicenseId, e.Reason, RevokedAtUtc = AsUtc(e.RevokedAtUtc) })
                .ToArray(),
            issuedAtUtc = AsUtc(bundle.IssuedAtUtc),
            version = bundle.Version,
        };

        var json = JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Unspecified: caller asserted UTC by stuffing it into a "*Utc"
        // property. Tag the Kind so the JSON serialiser emits the trailing
        // "Z" and issuer / verifier produce byte-identical output.
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
