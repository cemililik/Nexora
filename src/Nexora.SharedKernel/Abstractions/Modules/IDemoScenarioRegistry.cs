namespace Nexora.SharedKernel.Abstractions.Modules;

/// <summary>
/// A pre-built demo scenario advertised to the admin UI + CLI (T-029).
/// The registry holds the catalogue; modules contribute their own
/// scenarios (or extend existing ones) via <see cref="IDemoScenarioContribution"/>
/// implementations resolved from DI at composition time.
/// </summary>
/// <param name="Name">
/// Stable kebab-case identifier (e.g. <c>general</c>, <c>ngo</c>) — flows
/// through to <c>TenantDemoSeedContext.Scenario</c> and is the join key
/// every module's <see cref="IModule.SeedDemoDataAsync"/> branches on.
/// </param>
/// <param name="DescriptionLockey">
/// Lockey resolving to a one-line operator-facing description rendered
/// next to the dropdown option in the admin "Create Demo Environment"
/// dialog. Must exist in both en + tr bundles.
/// </param>
/// <param name="RequiredModules">
/// Module names (matching <see cref="IModule.Name"/>) that MUST be
/// installed for the scenario to be selectable. The registry filters
/// scenarios whose required modules are not all installed before
/// returning them to the admin dialog — a tenant without CRM never sees
/// a CRM-dependent scenario in the dropdown.
/// </param>
/// <param name="OptionalModules">
/// Module names that the scenario seeds when present but does not
/// require. Documented but not enforced by the registry (the scenario
/// loads regardless); each module's <c>SeedDemoDataAsync</c> decides
/// whether to contribute when invoked under this scenario.
/// </param>
public sealed record DemoScenario(
    string Name,
    string DescriptionLockey,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> OptionalModules);

/// <summary>
/// Module-side contribution that the platform collects at composition
/// time to populate <see cref="IDemoScenarioRegistry"/>. A single module
/// can register more than one scenario (e.g. a future "education" module
/// may add a <c>school-year</c> scenario alongside the platform defaults).
/// </summary>
public interface IDemoScenarioContribution
{
    /// <summary>Scenarios contributed by this module — invoked once at registry construction.</summary>
    IEnumerable<DemoScenario> GetScenarios();
}

/// <summary>
/// Catalogue of <see cref="DemoScenario"/>s available to the admin UI
/// + CLI. Filters out scenarios whose <see cref="DemoScenario.RequiredModules"/>
/// are not all installed for the queried tenant — the admin dialog must
/// not advertise a scenario the operator cannot actually run.
/// </summary>
public interface IDemoScenarioRegistry
{
    /// <summary>
    /// Returns every scenario applicable to <paramref name="tenantId"/>
    /// — scenarios whose <see cref="DemoScenario.RequiredModules"/> are
    /// all installed for the tenant. Empty list is a legitimate response
    /// (no module supports any scenario yet); callers render an empty
    /// state.
    /// </summary>
    Task<IReadOnlyList<DemoScenario>> GetForTenantAsync(
        string tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns every registered scenario regardless of installed-module
    /// state — used by architecture / drift tests that scan the whole
    /// catalogue, NOT by the admin dialog.
    /// </summary>
    IReadOnlyList<DemoScenario> GetAll();
}
