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
    /// Lifecycle stage. Mutable so the orchestrator can advance the row from
    /// <see cref="DemoSeedMarkerStatus.InProgress"/> to
    /// <see cref="DemoSeedMarkerStatus.Seeded"/> in a single SaveChanges per
    /// stage.
    /// </summary>
    public DemoSeedMarkerStatus Status { get; set; } = DemoSeedMarkerStatus.Seeded;

    /// <summary>
    /// UTC timestamp of the most recent state transition. Used by operators
    /// debugging stuck InProgress markers ("when did this seed last try?").
    /// Always set in UTC; never local time.
    /// </summary>
    public DateTimeOffset SeededAt { get; set; }
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
