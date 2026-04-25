using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        // Canonical form: alphabetical property order, no indentation,
        // explicit UTC for DateTime so issuer / verifier produce byte-
        // identical output. The signature is computed over these exact
        // bytes; any whitespace or property-order divergence would break
        // verification even on bit-identical data.
        WriteIndented = false,
        DictionaryKeyPolicy = null,
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
        catch (FormatException)
        {
            // Base64 garbage in the Signature field — tamper or transport corruption.
            return false;
        }
        catch (CryptographicException)
        {
            // Bad PEM, wrong key length, key/curve mismatch.
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
        var canonical = new
        {
            entries = bundle.Entries
                .Select(e => new { e.LicenseId, e.Reason, RevokedAtUtc = e.RevokedAtUtc.ToUniversalTime() })
                .ToArray(),
            issuedAtUtc = bundle.IssuedAtUtc.ToUniversalTime(),
            version = bundle.Version,
        };

        var json = JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }
}
