namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Reads platform-level policy caps for a compliance configuration key. The SaaS
/// implementation (<c>NmpComplianceCapProvider</c>, shipped in NMP.2) reads from the
/// license-cache entitlements channel. The dev / on-prem implementation
/// (<c>NullComplianceCapProvider</c>) returns a permissive cap for every key.
/// </summary>
/// <remarks>
/// See ADR-0025 §Implementation notes and <c>docs/architecture/MANAGEMENT_PORTAL.md</c>
/// for the NMP → CRM caps wire format.
/// </remarks>
public interface IComplianceCapProvider
{
    /// <summary>
    /// Returns the active cap for <paramref name="key"/> in the caller's tenant context.
    /// Returns <see cref="ComplianceCap.Permissive"/> when the provider has no policy
    /// defined for the key.
    /// </summary>
    Task<ComplianceCap> GetCapAsync(string key, CancellationToken ct = default);
}
