using Nexora.SharedKernel.Abstractions.Configuration;

namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Default <see cref="IComplianceCapProvider"/> for development and on-prem deployments
/// where no external policy authority exists. Returns <see cref="ComplianceCap.Permissive"/>
/// for every key — org overrides are unconstrained.
/// </summary>
/// <remarks>
/// Replaced by <c>NmpComplianceCapProvider</c> in SaaS deployments (NMP.2).
/// See ADR-0025 and <c>docs/architecture/MANAGEMENT_PORTAL.md</c>.
/// </remarks>
public sealed class NullComplianceCapProvider : IComplianceCapProvider
{
    /// <inheritdoc />
    public Task<ComplianceCap> GetCapAsync(string key, CancellationToken ct = default)
        => Task.FromResult(ComplianceCap.Permissive);
}
