namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// Describes how the Nexora instance is deployed.
/// </summary>
/// <remarks>
/// Configured via <c>appsettings.json → "Nexora:DeploymentMode"</c>.
/// Defaults to <see cref="OnPrem"/> so that development and self-hosted environments
/// work without any NMP connectivity.
/// </remarks>
public enum DeploymentMode
{
    /// <summary>
    /// Self-hosted on-premises deployment. NMP connectivity is optional.
    /// License verification falls back to <c>NullLicenseVerifier</c> (always allowed).
    /// </summary>
    OnPrem,

    /// <summary>
    /// Nexora-managed SaaS deployment. Module entitlements are enforced
    /// by the Nexora Management Portal (NMP) license service.
    /// </summary>
    SaaS
}
