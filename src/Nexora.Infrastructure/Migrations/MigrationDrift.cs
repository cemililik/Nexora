namespace Nexora.Infrastructure.Migrations;

/// <summary>
/// T-013: forensic record written to <c>platform_migration_drift</c>
/// whenever <c>PlatformAuditMigrationDriftJob</c> detects that a tenant
/// schema's applied migration head differs from the platform assembly's
/// known head for a given module.
/// </summary>
/// <remarks>
/// One row per (tenant, module) pair per drift-detection sweep.
/// Append-only log — no updates. Operators query this for triage:
/// <c>SELECT * FROM platform_migration_drift WHERE TenantId = X ORDER BY DetectedAtUtc DESC</c>.
/// </remarks>
public sealed class MigrationDrift
{
    /// <summary>Surrogate primary key — drift can recur across nightly sweeps.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Tenant whose schema diverged from the assembly head.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Module name (<c>IModule.Name</c>) whose head diverged.</summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// Migration head the platform assembly carries — i.e. what
    /// <c>DbContext.Database.GetMigrations()</c> returns. <see langword="null"/>
    /// when the assembly has no migrations (Phase 1.5 modules).
    /// </summary>
    public string? KnownHead { get; init; }

    /// <summary>
    /// Migration head actually present in the tenant schema. <see langword="null"/>
    /// when the schema has no <c>__EFMigrationsHistory</c> rows yet.
    /// </summary>
    public string? AppliedHead { get; init; }

    /// <summary>UTC timestamp when the audit sweep detected the drift.</summary>
    public DateTime DetectedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Factory matching the audit job's call shape.</summary>
    public static MigrationDrift Create(
        Guid tenantId, string moduleName, string? knownHead, string? appliedHead) =>
        new()
        {
            TenantId = tenantId,
            ModuleName = moduleName,
            KnownHead = knownHead,
            AppliedHead = appliedHead,
        };
}
