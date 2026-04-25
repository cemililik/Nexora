using System.Security.Cryptography;
using System.Text.Json;
using Nexora.Infrastructure.Licensing;
using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Tests.Licensing;

/// <summary>
/// T-015 verifier tests. Covers the AC "signature tampering rejected"
/// directly. The verifier is pure (no I/O) so we can build a real
/// RSA keypair, sign a known bundle, and assert tampered variants fail.
/// </summary>
public sealed class RevocationListVerifierTests
{
    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        var bundle = SignBundle(BuildBundle(), privateKey);

        RevocationListVerifier.Verify(bundle, publicKey).Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedEntries_ReturnsFalse()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        var original = SignBundle(BuildBundle(), privateKey);

        // Forge a new entry while keeping the original signature — what an
        // attacker would do if the wire payload is mutable.
        var tampered = new RevocationListBundle
        {
            Version = original.Version,
            IssuedAtUtc = original.IssuedAtUtc,
            Entries = original.Entries.Concat(
                [new RevokedLicenseEntry("forged-license", DateTime.UtcNow, "forged")]).ToArray(),
            Signature = original.Signature,
        };

        RevocationListVerifier.Verify(tampered, publicKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedIssuedAt_ReturnsFalse()
    {
        var (privateKey, publicKey) = GenerateKeyPair();
        var original = SignBundle(BuildBundle(), privateKey);

        var tampered = new RevocationListBundle
        {
            Version = original.Version,
            IssuedAtUtc = original.IssuedAtUtc.AddDays(1),
            Entries = original.Entries,
            Signature = original.Signature,
        };

        RevocationListVerifier.Verify(tampered, publicKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_GarbageSignature_ReturnsFalse()
    {
        var (_, publicKey) = GenerateKeyPair();
        var bundle = BuildBundle() with { Signature = "not-base64-***" };

        // FormatException is caught internally; verifier returns false.
        RevocationListVerifier.Verify(bundle, publicKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        var (privateKey, _) = GenerateKeyPair();
        var (_, otherPublicKey) = GenerateKeyPair();
        var bundle = SignBundle(BuildBundle(), privateKey);

        RevocationListVerifier.Verify(bundle, otherPublicKey).Should().BeFalse();
    }

    [Fact]
    public void Verify_EmptyBundle_StillVerifiesWhenSigned()
    {
        // No revocations is a valid steady state; signature still computed
        // over the empty entries array and must round-trip.
        var (privateKey, publicKey) = GenerateKeyPair();
        var bundle = BuildBundle() with { Entries = Array.Empty<RevokedLicenseEntry>() };
        var signed = SignBundle(bundle, privateKey);

        RevocationListVerifier.Verify(signed, publicKey).Should().BeTrue();
    }

    // --- helpers ---------------------------------------------------------

    private static RevocationListBundle BuildBundle()
    {
        return new RevocationListBundle
        {
            Version = 1,
            IssuedAtUtc = new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc),
            Entries =
            [
                new RevokedLicenseEntry("lic-001", new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc), "expired"),
                new RevokedLicenseEntry("lic-002", new DateTime(2026, 4, 24, 11, 0, 0, DateTimeKind.Utc), "fraud"),
            ],
            Signature = "<placeholder>",
        };
    }

    private static RevocationListBundle SignBundle(RevocationListBundle bundle, string privateKeyPem)
    {
        var canonicalBytes = RevocationListVerifier.ComputeCanonicalBytes(bundle);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var sig = rsa.SignData(canonicalBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return bundle with { Signature = Convert.ToBase64String(sig) };
    }

    private static (string Private, string Public) GenerateKeyPair()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem());
    }
}

