namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// Orchestrates demo-data seeding across installed modules (T-005 foundation for
/// the Phase 1.5.7 Demo Data Framework — T-006 CLI / T-008 admin UI call into this).
///
/// <para>
/// Walks <see cref="IModule"/>s in dependency order, opens a scope per module,
/// calls <see cref="IModule.SeedDemoDataAsync"/>, and records per-module
/// completion markers in the tenant's <c>demo_seed_markers</c> table so
/// re-invocation is a safe no-op for already-seeded modules.
/// </para>
/// </summary>
public interface IDemoDataSeeder
{
    /// <summary>
    /// Seeds demo content for the given <paramref name="tenantId"/> under
    /// <paramref name="scenario"/>. Re-running with the same
    /// <c>(tenantId, scenario)</c> is idempotent per module: already-seeded
    /// modules are skipped based on the marker table.
    /// </summary>
    /// <returns>
    /// Per-module outcome (seeded vs. already-seeded vs. error) so callers can
    /// surface a useful summary to operators.
    /// </returns>
    Task<DemoSeedRunResult> SeedAsync(
        string tenantId, string scenario, CancellationToken ct = default);
}

/// <summary>
/// Summary of a demo-seed run — one entry per module. Consumed by CLI (T-006)
/// and admin UI (T-008) to show the operator what happened.
/// </summary>
public sealed record DemoSeedRunResult(
    string TenantId,
    string Scenario,
    IReadOnlyList<DemoSeedModuleOutcome> Modules);

/// <summary>Outcome for a single module within a demo-seed run.</summary>
public sealed record DemoSeedModuleOutcome(
    string ModuleName,
    DemoSeedStatus Status,
    string? ErrorMessage = null);

/// <summary>Per-module demo-seed outcome status.</summary>
public enum DemoSeedStatus
{
    /// <summary>The module just seeded its demo content for this (tenant, scenario).</summary>
    Seeded,

    /// <summary>The module's marker already existed; the call was a no-op.</summary>
    AlreadySeeded,

    /// <summary>The module does not implement <see cref="IModule.SeedDemoDataAsync"/>.</summary>
    NoOp,

    /// <summary>The module threw; the orchestrator marked the run failed for this module.</summary>
    Failed
}
