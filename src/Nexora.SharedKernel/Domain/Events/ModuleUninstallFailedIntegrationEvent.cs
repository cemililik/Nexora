namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Published once when a cascade-uninstall sequence fails partway through
/// (T-026 / ADR-0031). Carries the in-memory forward log so consumers can
/// reconstruct which modules were already unwound and which one tripped.
/// </summary>
/// <remarks>
/// <para>
/// The earlier <c>ModuleUninstalledIntegrationEvent</c> rows for each
/// successful module remain in the outbox — this event does NOT replace
/// them; it adds the cascade-level failure context that no single per-
/// module event can express.
/// </para>
/// </remarks>
public sealed record ModuleUninstallFailedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Tenant whose cascade uninstall failed.</summary>
    public required Guid TenantIdGuid { get; init; }

    /// <summary>Root module the cascade was targeting.</summary>
    public required string TargetModuleName { get; init; }

    /// <summary>Module whose per-module transaction rolled back.</summary>
    public required string FailedModuleName { get; init; }

    /// <summary>Lockey that identifies the failure mode for UI surfacing.</summary>
    public required string ErrorLockey { get; init; }

    /// <summary>
    /// Modules whose per-module uninstall already committed before the
    /// failing step. Per ADR-0031 these are NOT auto-unwound — operators
    /// reinstall within retention to recover. Required so producers cannot
    /// silently emit an event with no forward log.
    /// </summary>
    public required IReadOnlyList<string> SuccessfulModulesSoFar { get; init; }

    // NOTE: <c>FailedAtUtc</c> intentionally absent — see the matching
    // note on <see cref="ModuleUninstalledIntegrationEvent"/>; consumers
    // read <see cref="IntegrationEventBase.OccurredAt"/>.
}
