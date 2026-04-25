using Microsoft.Extensions.DependencyInjection;
using Nexora.Infrastructure.Modules;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Modules;

/// <summary>
/// T-029 unit tests for <see cref="InMemoryDemoScenarioRegistry"/> —
/// scenario filtering by installed-module set + idempotent registration
/// + invalid-tenant-id guard.
/// </summary>
public sealed class InMemoryDemoScenarioRegistryTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void GetAll_PreSeedsGeneralAndNgo()
    {
        var registry = BuildRegistry();
        var all = registry.GetAll();
        all.Select(s => s.Name).Should().BeEquivalentTo(new[] { "general", "ngo" },
            "the platform-default scenarios are pre-seeded by the registry constructor.");
    }

    [Fact]
    public void Constructor_ContributionsAreAppendedAfterDefaults()
    {
        var contribution = Substitute.For<IDemoScenarioContribution>();
        contribution.GetScenarios().Returns(new[]
        {
            new DemoScenario("custom", "lockey_x", new[] { "contacts" }, Array.Empty<string>()),
        });

        var registry = BuildRegistry(contribution);
        var names = registry.GetAll().Select(s => s.Name).ToList();
        names.Should().Contain("custom");
        // Order matters — contributions append AFTER the platform defaults.
        // Without this assertion the test would also pass if a contribution
        // ever shadowed the default ordering (review round-2 finding).
        names.IndexOf("custom").Should().BeGreaterThan(names.IndexOf("general"));
        names.IndexOf("custom").Should().BeGreaterThan(names.IndexOf("ngo"));
    }

    [Fact]
    public void Constructor_DuplicateScenarioName_FirstWinsIsIdempotent()
    {
        // A contribution that re-emits an already-known scenario name
        // (e.g. tries to override `general`) is silently ignored — the
        // platform default keeps its registration.
        var contribution = Substitute.For<IDemoScenarioContribution>();
        contribution.GetScenarios().Returns(new[]
        {
            new DemoScenario("general", "lockey_overridden", new[] { "contacts" }, Array.Empty<string>()),
        });

        var registry = BuildRegistry(contribution);
        var general = registry.GetAll().Single(s => s.Name == "general");
        general.DescriptionLockey.Should().Be(
            "lockey_identity_tenants_demo_scenario_general_description",
            "first registration wins — a module contribution must not silently override the platform default.");
    }

    [Fact]
    public async Task GetForTenantAsync_NoModuleAvailability_ReturnsAllScenarios()
    {
        // The Phase 1.5 fallback: if no IModuleAvailability is registered,
        // every scenario is visible (matches the seeder/cleaner pattern).
        var registry = BuildRegistry();
        var result = await registry.GetForTenantAsync(_tenantId.ToString());
        result.Should().HaveCount(2,
            "without IModuleAvailability the registry returns every scenario unfiltered.");
    }

    [Fact]
    public async Task GetForTenantAsync_AvailabilityRegistered_FiltersByInstalledModules()
    {
        // When the availability service is wired, scenarios whose
        // RequiredModules are not all installed are filtered out.
        var availability = Substitute.For<IModuleAvailability>();
        availability.GetInstalledModulesAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)Array.Empty<string>());

        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<IModuleAvailability>(availability);
        services.AddSingleton<IEnumerable<IDemoScenarioContribution>>(Array.Empty<IDemoScenarioContribution>());
        services.AddSingleton<InMemoryDemoScenarioRegistry>();
        var sp = services.BuildServiceProvider();

        var registry = sp.GetRequiredService<InMemoryDemoScenarioRegistry>();
        var result = await registry.GetForTenantAsync(_tenantId.ToString());

        result.Should().BeEmpty(
            "every default scenario requires `contacts`; with `contacts` not installed both general and ngo must be filtered out.");
    }

    [Fact]
    public async Task GetForTenantAsync_InvalidTenantId_ThrowsArgumentExceptionWithLockey()
    {
        var registry = BuildRegistry();
        var act = async () => await registry.GetForTenantAsync("not-a-guid");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*lockey_identity_demo_data_tenant_id_must_be_guid*");
    }

    private static InMemoryDemoScenarioRegistry BuildRegistry(params IDemoScenarioContribution[] contributions)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddSingleton<IEnumerable<IDemoScenarioContribution>>(contributions);
        var sp = services.BuildServiceProvider();
        return new InMemoryDemoScenarioRegistry(
            sp.GetRequiredService<IServiceScopeFactory>(),
            contributions);
    }
}
