namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// T-014: emitted when <c>LicenseReloadService</c> successfully validates
/// a changed license file and swaps the in-memory snapshot. Consumed by
/// admin-portal notifications + ops dashboards so operators can see when
/// a license actually became live.
/// </summary>
public sealed record LicenseRefreshedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Identifier of the newly-active license.</summary>
    public required string LicenseId { get; init; }

    /// <summary>Tier of the newly-active license.</summary>
    public required string Tier { get; init; }

    /// <summary>UTC timestamp at which the new license expires.</summary>
    public required DateTime ValidUntilUtc { get; init; }

    /// <summary>UTC timestamp the snapshot was loaded.</summary>
    public required DateTime LoadedAtUtc { get; init; }
}

/// <summary>
/// T-014: emitted when <c>LicenseReloadService</c> sees a file change but
/// validation fails (bad signature, expired license, malformed JSON).
/// The previous valid snapshot is retained — this event is the alert
/// path so the admin sees the failure and can re-upload a good file.
/// </summary>
public sealed record LicenseRefreshFailedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Lockey describing the failure (for UI surfacing).</summary>
    public required string ErrorLocalizationKey { get; init; }

    /// <summary>Diagnostic-only description for ops triage; not user-facing.</summary>
    public required string ErrorReason { get; init; }

    /// <summary>UTC timestamp the failed reload was attempted.</summary>
    public required DateTime FailedAtUtc { get; init; }
}
