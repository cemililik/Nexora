namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-014: validates raw license-file bytes against deployment trust
/// material (signing key, expiry, schema). Pluggable so the on-prem track
/// (signed-JSON file) and the SaaS / NMP track (NMP API echo) can swap
/// implementations without changing the reload service.
/// </summary>
public interface ILicenseValidator
{
    /// <summary>
    /// Reads <paramref name="fileBytes"/> and returns a populated
    /// <see cref="LicenseValidationResult"/>. MUST NOT throw on a bad
    /// file — any parsing / signature / expiry failure is reported via
    /// <see cref="LicenseValidationResult.IsValid"/> and
    /// <see cref="LicenseValidationResult.ErrorReason"/> so the caller can
    /// log + roll back to the previous snapshot.
    /// </summary>
    Task<LicenseValidationResult> ValidateAsync(ReadOnlyMemory<byte> fileBytes, CancellationToken ct);
}

/// <summary>
/// Result of a single validation attempt. Either <see cref="Snapshot"/>
/// is non-null (valid) or <see cref="ErrorReason"/> is non-null (invalid).
/// Never both, never neither.
/// </summary>
public sealed record LicenseValidationResult
{
    /// <summary>Validated, in-memory snapshot — present when <see cref="IsValid"/> is true.</summary>
    public LicenseSnapshot? Snapshot { get; init; }

    /// <summary>
    /// Lockey + reason pair. Lockey for UI surfacing, reason for diagnostic
    /// logging. Present when validation failed.
    /// </summary>
    public string? ErrorLocalizationKey { get; init; }

    /// <summary>Diagnostic-only error description, never surfaced to end users.</summary>
    public string? ErrorReason { get; init; }

    /// <summary>True when <see cref="Snapshot"/> is set.</summary>
    public bool IsValid => Snapshot is not null;

    /// <summary>Builds a successful result.</summary>
    public static LicenseValidationResult Valid(LicenseSnapshot snapshot) =>
        new() { Snapshot = snapshot };

    /// <summary>Builds a failed result.</summary>
    public static LicenseValidationResult Invalid(string localizationKey, string reason) =>
        new() { ErrorLocalizationKey = localizationKey, ErrorReason = reason };
}
