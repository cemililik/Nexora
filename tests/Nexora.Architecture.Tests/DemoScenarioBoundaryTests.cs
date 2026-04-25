using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Modules;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-029 architecture guard: every module declared in any registered
/// <see cref="DemoScenario.RequiredModules"/> must implement
/// <see cref="IModule.SeedDemoDataAsync"/> with non-default content
/// (i.e. <see cref="DemoDataSeeder.IsDefaultNoOp"/> returns <c>false</c>).
///
/// <para>
/// Catches "registered but inert" drift: a future module landing in
/// `RequiredModules` for the `general` or `ngo` scenario without anyone
/// wiring up its <c>SeedDemoDataAsync</c> means the dialog will offer
/// the scenario to operators but the seed will silently produce nothing
/// for that module. This test fires CI BEFORE that lands in production.
/// </para>
///
/// <para>
/// The test is module-agnostic: it walks every loaded
/// <see cref="IModule"/> assembly, builds the registry the same way
/// production DI does, and asserts the invariant. New modules + new
/// scenarios are picked up automatically with no per-module plumbing
/// in this file.
/// </para>
/// </summary>
public sealed class DemoScenarioBoundaryTests
{
    [Fact]
    public void EveryRegisteredScenario_RequiredModules_HaveNonDefaultSeedDemoDataAsync()
    {
        // Force-load every Nexora.Modules.* assembly so AppDomain
        // enumeration sees them — same trick as
        // SoftDeleteFilterBoundaryTests.ModuleDbContexts.
        _ = typeof(Nexora.Modules.Contacts.ContactsModule).Assembly;
        _ = typeof(Nexora.Modules.Documents.DocumentsModule).Assembly;
        _ = typeof(Nexora.Modules.Notifications.NotificationsModule).Assembly;
        _ = typeof(Nexora.Modules.Reporting.ReportingModule).Assembly;
        _ = typeof(Nexora.Modules.Audit.AuditModule).Assembly;
        _ = typeof(Nexora.Modules.Identity.IdentityModule).Assembly;

        var modules = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } n &&
                        n.StartsWith("Nexora.Modules.", StringComparison.Ordinal))
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(IModule).IsAssignableFrom(t))
            .Select(t => (IModule)Activator.CreateInstance(t)!)
            .ToList();

        // Build the registry exactly the way production DI does — no
        // contributions are wired in this test (module contributions
        // arrive with their own owning-module's registration; the
        // platform defaults `general` + `ngo` are enough to exercise
        // the invariant against today's modules).
        var services = new ServiceCollection()
            .AddSingleton<IEnumerable<IDemoScenarioContribution>>(Array.Empty<IDemoScenarioContribution>())
            .BuildServiceProvider();
        var registry = new InMemoryDemoScenarioRegistry(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IEnumerable<IDemoScenarioContribution>>());

        var modulesByName = modules.ToDictionary(
            m => m.Name, StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();
        foreach (var scenario in registry.GetAll())
        {
            foreach (var requiredModule in scenario.RequiredModules)
            {
                // The scenario references a module that doesn't exist
                // in the loaded set — surface as an offender (the
                // dialog would silently filter the scenario out for
                // every tenant, defeating its purpose).
                if (!modulesByName.TryGetValue(requiredModule, out var module))
                {
                    offenders.Add(
                        $"  - scenario '{scenario.Name}' requires module '{requiredModule}' " +
                        "but no IModule with that name is loaded.");
                    continue;
                }

                if (DemoDataSeeder.IsDefaultNoOp(module))
                {
                    offenders.Add(
                        $"  - scenario '{scenario.Name}' requires module '{requiredModule}' " +
                        "but its IModule.SeedDemoDataAsync is the default no-op DIM. " +
                        "Either implement SeedDemoDataAsync with content for this scenario " +
                        "OR remove the module from the scenario's RequiredModules.");
                }
            }
        }

        offenders.Should().BeEmpty(
            "every module in a scenario's RequiredModules MUST ship non-default seed content for that scenario; otherwise the admin dialog promises an outcome the seed cannot deliver. Offenders:\n" +
            string.Join("\n", offenders));
    }
}
