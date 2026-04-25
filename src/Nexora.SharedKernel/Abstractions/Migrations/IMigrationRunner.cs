namespace Nexora.SharedKernel.Abstractions.Migrations;

/// <summary>
/// Platform-level orchestrator that applies every registered module's
/// schema migrations to a tenant schema in module-dependency order.
/// Wraps the per-module
/// <see cref="MultiTenancy.IModuleMigration"/> mechanism with the
/// concurrency, ordering, and failure-tracking discipline described in
/// <c>docs/operations/migration-orchestration.md</c> §2.
///
/// <para>
/// <b>Per-tenant lock.</b> Concurrent invocations against the same
/// tenant are serialised via PostgreSQL session-level
/// <c>pg_try_advisory_lock</c> over a stable per-tenant key (FNV-1a-64
/// of <c>"migrate:" + tenantId</c>). The runner polls with a short
/// retry budget instead of blocking; if the lock is busy the
/// returned <see cref="MigrationRunResult"/> carries
/// <see cref="MigrationRunStatus.LockNotAcquired"/> and the caller
/// retries later (review #69).
/// </para>
///
/// <para>
/// <b>Failure handling.</b> When any module's migration throws, the
/// runner stops, persists a row to <c>platform_migration_failures</c>
/// with the offending module + exception detail, transitions the
/// tenant to <c>TenantStatus.MigrationFailed</c> (gating subsequent
/// requests at the API layer), and surfaces a
/// <see cref="MigrationRunResult"/> with <c>Failed</c> status. The
/// tenant does NOT auto-retry; ops follow
/// <c>docs/operations/migration-orchestration.md</c> §2.3.
/// </para>
/// </summary>
public interface IMigrationRunner
{
    /// <summary>
    /// Applies every registered module's migration head to the named
    /// tenant schema in module-dependency order. Returns a structured
    /// result describing the outcome — exceptions are captured and
    /// returned via <see cref="MigrationRunResult"/>, not thrown
    /// (callers like the platform-upgrade Hangfire job iterate over
    /// many tenants and need a per-tenant outcome record, not an
    /// abort cascade).
    /// </summary>
    /// <param name="tenantId">
    /// The tenant id (raw GUID string) — drives both the schema name
    /// (<c>tenant_{tenantId}</c>) and the advisory-lock key.
    /// </param>
    /// <param name="ct">Cancellation token plumbed through every step.</param>
    Task<MigrationRunResult> MigrateAllModulesAsync(
        string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Per-tenant outcome of a <see cref="IMigrationRunner.MigrateAllModulesAsync"/>
/// invocation.
/// </summary>
public sealed record MigrationRunResult(
    string TenantId,
    MigrationRunStatus Status,
    IReadOnlyList<MigrationModuleOutcome> Modules,
    string? FailureModuleName = null,
    string? FailureMessage = null);

/// <summary>Per-module outcome inside a <see cref="MigrationRunResult"/>.</summary>
public sealed record MigrationModuleOutcome(
    string ModuleName,
    MigrationModuleStatus Status,
    string? ErrorMessage = null);

/// <summary>Top-level outcome of a tenant migration run.</summary>
public enum MigrationRunStatus
{
    /// <summary>Every module migrated cleanly.</summary>
    Succeeded,

    /// <summary>One module failed; the tenant is now quarantined as <c>MigrationFailed</c>.</summary>
    Failed,

    /// <summary>
    /// Could not acquire the per-tenant advisory lock within the configured
    /// timeout — another runner is already migrating this tenant. Caller
    /// should retry later.
    /// </summary>
    LockNotAcquired
}

/// <summary>Per-module outcome status inside a tenant migration run.</summary>
public enum MigrationModuleStatus
{
    /// <summary>Module migrated cleanly (or had no pending migrations).</summary>
    Migrated,

    /// <summary>Module's <c>MigrateAsync</c> threw — see <c>ErrorMessage</c>.</summary>
    Failed,

    /// <summary>
    /// Skipped because a prior module in the same run failed; modules
    /// downstream of a failure do not run so a partial-success state is
    /// not silently introduced.
    /// </summary>
    Skipped
}
