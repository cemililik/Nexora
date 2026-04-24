namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Idempotency marker for per-(tenant, module, scenario) demo-data seeding (T-005).
/// Written by <c>DemoDataSeeder</c> in two phases — <see cref="DemoSeedMarkerStatus.InProgress"/>
/// before the module's <c>SeedDemoDataAsync</c> runs, then promoted to
/// <see cref="DemoSeedMarkerStatus.Seeded"/> on success. Subsequent runs with the
/// same (TenantId, ModuleName, Scenario) short-circuit when Status is Seeded;
/// InProgress markers signal a prior crash and trigger a retry (modules MUST
/// be idempotent — see <c>IModule.SeedDemoDataAsync</c> docs).
/// </summary>
public sealed class DemoSeedMarker
{
    /// <summary>Tenant the markers row belongs to. Lives in the tenant schema.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>
    /// Module name as declared by <c>IModule.Name</c> (e.g. "contacts",
    /// "identity"). Composite key participant; case-sensitive in storage so
    /// the orchestrator's case-insensitive lookup is the boundary, not the DB.
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// Scenario identifier supplied by the caller (e.g. "general", "ngo").
    /// Free-form string — modules MAY support a subset and skip otherwise.
    /// Composite key participant.
    /// </summary>
    public required string Scenario { get; init; }

    /// <summary>
    /// Lifecycle stage. Defaults to <see cref="DemoSeedMarkerStatus.InProgress"/>
    /// because a freshly-constructed marker means "we are about to start" —
    /// defaulting to <see cref="DemoSeedMarkerStatus.Seeded"/> would silently
    /// mark un-run seeds as complete, which is the opposite of the contract.
    /// Setter is <c>internal</c> so only <c>DemoDataSeeder</c> (same assembly)
    /// can advance the row from InProgress to Seeded — external code reads
    /// the marker via the orchestrator API, never mutates it directly.
    /// </summary>
    public DemoSeedMarkerStatus Status { get; internal set; } = DemoSeedMarkerStatus.InProgress;

    /// <summary>
    /// UTC timestamp of the InProgress transition (i.e., when the orchestrator
    /// committed to running the module's seed). Always set in UTC; never local.
    /// Distinct from <see cref="CompletedAt"/> so an operator inspecting a
    /// stuck InProgress marker can answer "when did this attempt start?" and
    /// "did it ever complete?" with two separate columns.
    /// Setter is <c>internal</c> — see <see cref="Status"/> rationale.
    /// </summary>
    public DateTimeOffset StartedAt { get; internal set; }

    /// <summary>
    /// UTC timestamp when the marker was promoted to
    /// <see cref="DemoSeedMarkerStatus.Seeded"/>. <c>null</c> while
    /// <see cref="Status"/> is <see cref="DemoSeedMarkerStatus.InProgress"/>.
    /// Always set in UTC. Setter is <c>internal</c> — see <see cref="Status"/>
    /// rationale.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; internal set; }

    /// <summary>
    /// Factory for a freshly-started marker. Centralises the "new marker =
    /// InProgress with StartedAt = now, CompletedAt = null" invariant so
    /// callers cannot accidentally forget one of the timestamps.
    /// </summary>
    internal static DemoSeedMarker CreateInProgress(Guid tenantId, string moduleName, string scenario) =>
        new()
        {
            TenantId = tenantId,
            ModuleName = moduleName,
            Scenario = scenario,
            Status = DemoSeedMarkerStatus.InProgress,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = null,
        };
}

/// <summary>
/// Lifecycle states for a <see cref="DemoSeedMarker"/>. Distinct from
/// <see cref="DemoSeedStatus"/> (per-module run outcome) — the marker tracks
/// what the database remembers; the run outcome tracks what the orchestrator
/// reported to the caller.
/// </summary>
public enum DemoSeedMarkerStatus
{
    /// <summary>The seeder wrote a marker before invoking the module; the module either crashed or is still running. Future runs retry.</summary>
    InProgress = 0,

    /// <summary>The module finished cleanly and the marker was promoted. Future runs short-circuit.</summary>
    Seeded = 1
}
