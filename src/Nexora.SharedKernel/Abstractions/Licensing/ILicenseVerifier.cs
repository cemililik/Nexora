namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// Verifies whether a tenant is licensed to use a given module.
/// </summary>
/// <remarks>
/// In Phase 1.5.2 the only implementation is <c>NullLicenseVerifier</c>, which always
/// grants access. The NMP-backed implementation (<c>NmpLicenseVerifier</c>) will be
/// introduced in the NMP track.
/// </remarks>
public interface ILicenseVerifier
{
    /// <summary>
    /// Returns <see langword="true"/> when the tenant holds a valid license for the module.
    /// </summary>
    Task<bool> IsLicensedAsync(Guid tenantId, string moduleName, CancellationToken ct = default);
}
