namespace Nexora.SharedKernel.Abstractions.Licensing;

/// <summary>
/// T-014: read-side of the hot-reloaded license snapshot. Hot-path
/// callers consult this synchronously to gate module access without
/// touching disk. The implementation is fed by <c>LicenseReloadService</c>
/// (background polling + SIGHUP-triggered reloads) which swaps snapshots
/// via <c>Interlocked.Exchange</c> so a mid-request reload never surfaces
/// a torn intermediate.
/// </summary>
public interface ILicenseProvider
{
    /// <summary>
    /// Currently-loaded snapshot, or <see langword="null"/> when no valid
    /// license has loaded yet (boot before first poll, or all reloads
    /// failed). Hot-path callers MUST handle the null case — fail closed
    /// (deny access) is the convention so a missing license file does not
    /// silently grant unrestricted use.
    /// </summary>
    LicenseSnapshot? Current { get; }
}
