namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Orchestrates demo-data cleanup across installed modules (T-009 follow-up to
/// T-005's <see cref="IDemoDataSeeder"/>). The CLI verb <c>demo:clean</c>
/// (T-006 dispatcher) and the admin-UI cleanup button (T-008) both drive this.
///
/// <para>
/// Walks <see cref="IModule"/>s in <b>reverse</b> dependency order, opens a
/// scope per module, calls <see cref="IModule.CleanDemoDataAsync"/>, and then
/// deletes the matching rows from <c>demo_seed_markers</c> so a subsequent
/// <see cref="IDemoDataSeeder.SeedAsync"/> run does not short-circuit on a
/// stale "Seeded" marker for a tenant that has since been wiped.
/// </para>
///
/// <para>
/// The <c>--drop-tenant</c> CLI path takes a different route entirely:
/// it drops the whole tenant schema via
/// <see cref="DropTenantAsync"/> and does NOT call module-level cleanup,
/// because the CASCADE drop is atomic and strictly cheaper than a
/// per-module delete pass. Module authors MUST NOT rely on
/// <see cref="IModule.CleanDemoDataAsync"/> running before tenant drop.
/// </para>
/// </summary>
public interface IDemoDataCleaner
{
    /// <summary>
    /// Cleans demo content for the given <paramref name="tenantId"/> under
    /// <paramref name="scenario"/>. Re-running is idempotent per module —
    /// modules implement <see cref="IModule.CleanDemoDataAsync"/> defensively
    /// (no-op on missing rows) and marker rows are removed only after the
    /// module's delete succeeds.
    /// </summary>
    /// <returns>Per-module outcome so callers can print a useful summary.</returns>
    Task<DemoCleanRunResult> CleanAsync(
        string tenantId, string scenario, CancellationToken ct = default);

    /// <summary>
    /// Drops the entire tenant schema (all modules, all tables — demo and
    /// real alike). Used only by <c>demo:clean --drop-tenant --yes</c> when
    /// the operator has explicitly confirmed a full teardown. Module-level
    /// <see cref="IModule.CleanDemoDataAsync"/> is NOT invoked; the schema
    /// DROP CASCADE removes every module's tables at once. Implementations
    /// SHOULD emit <c>TenantDeprovisionedIntegrationEvent</c> so downstream
    /// consumers (MinIO, Keycloak, cache layers) can clean their own
    /// tenant-scoped state.
    /// </summary>
    Task<DemoDropTenantResult> DropTenantAsync(
        string tenantId, CancellationToken ct = default);
}

/// <summary>
/// Summary of a demo-clean run — one entry per module. Shape mirrors
/// <see cref="DemoSeedRunResult"/> so CLI and admin UI can reuse formatters.
/// </summary>
public sealed record DemoCleanRunResult(
    string TenantId,
    string Scenario,
    IReadOnlyList<DemoCleanModuleOutcome> Modules);

/// <summary>Outcome for a single module within a demo-clean run.</summary>
public sealed record DemoCleanModuleOutcome(
    string ModuleName,
    DemoCleanStatus Status,
    string? ErrorMessage = null);

/// <summary>Per-module demo-clean outcome status.</summary>
public enum DemoCleanStatus
{
    /// <summary>
    /// The module ran its <see cref="IModule.CleanDemoDataAsync"/> and the
    /// matching marker row was removed. Idempotent — modules that are already
    /// clean still report <see cref="Cleaned"/>.
    /// </summary>
    Cleaned,

    /// <summary>
    /// No <c>Seeded</c> marker existed for this module/tenant/scenario, so
    /// there was nothing to clean. The module's own
    /// <see cref="IModule.CleanDemoDataAsync"/> was NOT invoked because the
    /// orchestrator has no evidence that a seed ever happened.
    /// </summary>
    NothingToClean,

    /// <summary>
    /// The module ships the default no-op <see cref="IModule.CleanDemoDataAsync"/>.
    /// Marker row (if present) is still removed so a future seed re-runs
    /// cleanly.
    /// </summary>
    NoOp,

    /// <summary>
    /// The module threw; neither its rows nor its marker are guaranteed to
    /// have been removed. Operator inspects the error and decides whether to
    /// rerun (cleanup is designed to be safe to retry).
    /// </summary>
    Failed
}

/// <summary>Result of <see cref="IDemoDataCleaner.DropTenantAsync"/>.</summary>
public sealed record DemoDropTenantResult(
    string TenantId,
    bool SchemaDropped,
    string? ErrorMessage = null);
