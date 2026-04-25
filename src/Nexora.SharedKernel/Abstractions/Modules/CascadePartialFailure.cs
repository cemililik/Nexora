namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Thrown by the cascade-uninstall orchestrator when a module step fails
/// partway through the cascade. Per ADR-0031 cascade uninstall is NOT
/// atomic across modules — earlier per-module transactions remain
/// committed and their tables remain renamed-to-<c>_del_</c>; the
/// operator's recovery primitive is reinstall-within-retention.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SuccessfulModules"/> mirrors the in-memory forward log the
/// orchestrator maintains; the same payload travels on the
/// <c>ModuleUninstallFailedIntegrationEvent</c> outbox event so external
/// consumers (admin UI, audit) see the partial state.
/// </para>
/// </remarks>
public sealed class CascadePartialFailure(
    string failedModuleName,
    string errorLockey,
    IReadOnlyList<string> successfulModules,
    Exception? innerException = null)
    : InvalidOperationException(
        $"Cascade uninstall failed at module '{failedModuleName}' (lockey: {errorLockey}). " +
        $"Earlier modules already uninstalled (forward log): " +
        $"[{string.Join(", ", successfulModules)}]. " +
        "Renamed _del_ tables remain — reinstall within retention to recover.",
        innerException)
{
    /// <summary>Module whose per-module transaction failed.</summary>
    public string FailedModuleName { get; } = failedModuleName;

    /// <summary>Lockey identifying the failure mode.</summary>
    public string ErrorLockey { get; } = errorLockey;

    /// <summary>
    /// Modules whose per-module uninstall transaction had already committed
    /// at the moment of failure. NOT rolled back per ADR-0031.
    /// </summary>
    public IReadOnlyList<string> SuccessfulModules { get; } = successfulModules;
}
