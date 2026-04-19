using Nexora.SharedKernel.Abstractions.Licensing;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// Always-allowed <see cref="ILicenseVerifier"/> used in development and on-premises deployments
/// where NMP license enforcement has not yet been configured.
/// </summary>
/// <remarks>
/// Replace with <c>NmpLicenseVerifier</c> (NMP track) for SaaS production deployments.
/// </remarks>
public sealed class NullLicenseVerifier : ILicenseVerifier
{
    /// <inheritdoc />
    public Task<bool> IsLicensedAsync(Guid tenantId, string moduleName, CancellationToken ct = default)
        => Task.FromResult(true);
}
