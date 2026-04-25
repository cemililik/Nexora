using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Default <see cref="IDemoScenarioRegistry"/> implementation (T-029).
/// Pre-seeds the platform-wide <c>general</c> + <c>ngo</c> scenarios at
/// construction and appends any <see cref="IDemoScenarioContribution"/>
/// registrations resolved from DI. Filters by installed modules via
/// <see cref="IModuleAvailability"/> when registered (mirrors the
/// orchestrator's pattern from <see cref="DemoDataSeeder.FilterInstalledAsync"/>).
///
/// <para>
/// Singleton-safe: scenarios are immutable records; the catalogue is
/// frozen at construction. Per-tenant filter calls take a fresh DI scope
/// to resolve <see cref="IModuleAvailability"/> against the tenant
/// context, so the singleton itself holds no per-tenant state.
/// </para>
///
/// <para>
/// Idempotent registration: a contribution that re-emits a scenario
/// with an already-known <see cref="DemoScenario.Name"/> is silently
/// ignored — the FIRST registration wins. This lets a future module
/// declare it "supports" the <c>ngo</c> scenario via
/// <see cref="DemoScenario.OptionalModules"/> without overwriting the
/// platform-default registration.
/// </para>
/// </summary>
public sealed class InMemoryDemoScenarioRegistry : IDemoScenarioRegistry
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<DemoScenario> _scenarios;

    public InMemoryDemoScenarioRegistry(
        IServiceScopeFactory scopeFactory,
        IEnumerable<IDemoScenarioContribution> contributions)
    {
        _scopeFactory = scopeFactory;

        var byName = new Dictionary<string, DemoScenario>(StringComparer.OrdinalIgnoreCase);

        // Platform defaults — kept in code (not in a contribution) because
        // they exist regardless of which modules ship and the admin
        // dialog's interim hardcoded `general`/`ngo` dropdown will keep
        // working until T-029's frontend swap.
        Register(byName, new DemoScenario(
            Name: "general",
            DescriptionLockey: "lockey_identity_tenants_demo_scenario_general_description",
            RequiredModules: new[] { "contacts" },
            OptionalModules: new[] { "crm", "finance", "subscription", "projects" }));

        Register(byName, new DemoScenario(
            Name: "ngo",
            DescriptionLockey: "lockey_identity_tenants_demo_scenario_ngo_description",
            RequiredModules: new[] { "contacts" },
            OptionalModules: new[] { "fundraising", "sponsorship", "events" }));

        foreach (var contribution in contributions)
        {
            foreach (var scenario in contribution.GetScenarios())
            {
                Register(byName, scenario);
            }
        }

        _scenarios = byName.Values.ToList();
    }

    private static void Register(IDictionary<string, DemoScenario> byName, DemoScenario scenario)
    {
        // Idempotent: first registration wins. Lets contributions advertise
        // they support an existing scenario without overwriting the
        // platform-default (their support shows up via OptionalModules
        // they advertise on the canonical scenario, not via re-registration).
        byName.TryAdd(scenario.Name, scenario);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemoScenario>> GetForTenantAsync(
        string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            throw new ArgumentException(
                "lockey_demo_data_tenant_id_must_be_guid", nameof(tenantId));
        }

        // Probe-without-scope first — the common Phase-1.5 path is "no
        // IModuleAvailability registered"; in that case every scenario is
        // visible (matches the seeder/cleaner fallback) and we skip the
        // tenant-context-bound async scope materialisation.
        await using (var probeScope = _scopeFactory.CreateAsyncScope())
        {
            if (probeScope.ServiceProvider.GetService<IModuleAvailability>() is null)
            {
                return _scenarios;
            }
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantGuid.ToString());
        var availability = scope.ServiceProvider.GetRequiredService<IModuleAvailability>();

        var installed = (await availability.GetInstalledModulesAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _scenarios
            .Where(s => s.RequiredModules.All(m => installed.Contains(m)))
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<DemoScenario> GetAll() => _scenarios;
}
