namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// T-013: emitted by <c>PlatformAuditMigrationDriftJob</c> when a tenant
/// schema's applied migration head differs from the platform assembly's
/// known head for at least one module. Consumed by ops alerting (Grafana
/// → PagerDuty / Slack) so operators can triage stalled or out-of-band
/// migrations before the affected tenant accumulates inconsistent data.
/// </summary>
/// <remarks>
/// One event is emitted per drift-detection sweep that finds at least one
/// drift row — not one event per drift row — so downstream alert
/// throttling stays simple (<c>{tenantCount, driftRowCount}</c> aggregates
/// in the payload). A separate <c>platform_migration_drift</c> table
/// carries the full per-row evidence (including the per-module breakdown)
/// for ops triage.
/// </remarks>
public sealed record MigrationDriftDetectedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Number of tenants in the sweep that had at least one drift row.</summary>
    public required int TenantCount { get; init; }

    /// <summary>Total drift rows recorded across all tenants in this sweep.</summary>
    public required int DriftRowCount { get; init; }

    /// <summary>UTC timestamp when the audit sweep completed.</summary>
    public required DateTime AuditCompletedAtUtc { get; init; }
}
