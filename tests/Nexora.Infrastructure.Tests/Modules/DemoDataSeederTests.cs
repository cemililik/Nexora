using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Modules;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Infrastructure.Tests.Modules;

/// <summary>
/// T-005: verifies <see cref="DemoDataSeeder"/> topological ordering and
/// per-(tenant, module, scenario) idempotency.
/// </summary>
public sealed class DemoDataSeederTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string Scenario = "general";

    [Fact]
    public async Task SeedAsync_OrdersModulesByDependencies()
    {
        var callLog = new List<string>();
        var identity = new FakeModule("identity", dependencies: Array.Empty<string>(), callLog);
        var contacts = new FakeModule("contacts", dependencies: new[] { "identity" }, callLog);
        var crm = new FakeModule("crm", dependencies: new[] { "identity", "contacts" }, callLog);

        // Intentionally pass in the WRONG order to prove the sort reorders.
        var seeder = BuildSeeder(crm, contacts, identity);

        var result = await seeder.SeedAsync(_tenantId.ToString(), Scenario);

        callLog.Should().ContainInOrder("identity", "contacts", "crm");
        result.Modules.Select(m => m.ModuleName).Should().ContainInOrder("identity", "contacts", "crm");
        result.Modules.Should().OnlyContain(o => o.Status == DemoSeedStatus.Seeded);
    }

    [Fact]
    public async Task SeedAsync_SecondRun_IsIdempotentNoOp()
    {
        var callLog = new List<string>();
        var module = new FakeModule("contacts", Array.Empty<string>(), callLog);
        var seeder = BuildSeeder(module);

        // First run — fresh, should seed.
        var first = await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        first.Modules.Single().Status.Should().Be(DemoSeedStatus.Seeded);
        callLog.Should().ContainSingle().Which.Should().Be("contacts");

        // Second run — marker present, must short-circuit without calling the module.
        var second = await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        second.Modules.Single().Status.Should().Be(DemoSeedStatus.AlreadySeeded);
        callLog.Should().ContainSingle("the module's SeedDemoDataAsync must not be invoked on the second run");
    }

    [Fact]
    public async Task SeedAsync_DifferentScenario_RunsAgain()
    {
        var callLog = new List<string>();
        var module = new FakeModule("contacts", Array.Empty<string>(), callLog);
        var seeder = BuildSeeder(module);

        var first = await seeder.SeedAsync(_tenantId.ToString(), "general");
        var second = await seeder.SeedAsync(_tenantId.ToString(), "ngo");

        first.Modules.Single().Status.Should().Be(DemoSeedStatus.Seeded);
        second.Modules.Single().Status.Should().Be(DemoSeedStatus.Seeded);
        callLog.Should().HaveCount(2, "marker is keyed by scenario so a different scenario re-runs the module");
    }

    [Fact]
    public async Task SeedAsync_ModuleThrows_ReturnsFailed_WithoutBlockingSiblings()
    {
        var callLog = new List<string>();
        var broken = new FakeModule("contacts", Array.Empty<string>(), callLog, throwOnSeed: true);
        var healthy = new FakeModule("documents", new[] { "contacts" }, callLog);
        var seeder = BuildSeeder(broken, healthy);

        var result = await seeder.SeedAsync(_tenantId.ToString(), Scenario);

        result.Modules.Should().HaveCount(2);
        result.Modules.Single(m => m.ModuleName == "contacts").Status.Should().Be(DemoSeedStatus.Failed);
        result.Modules.Single(m => m.ModuleName == "documents").Status.Should().Be(DemoSeedStatus.Seeded,
            "a broken sibling must not block the rest of the pipeline; the caller reads per-module outcomes instead");
    }

    [Fact]
    public void OrderByDependencies_Cycle_Throws()
    {
        var a = new FakeModule("a", new[] { "b" }, new List<string>());
        var b = new FakeModule("b", new[] { "a" }, new List<string>());

        var act = () => DemoDataSeeder.OrderByDependencies(new List<IModule> { a, b });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Cycle detected*");
    }

    [Fact]
    public void OrderByDependencies_MissingDependency_Throws()
    {
        var orphan = new FakeModule("crm", new[] { "subscription" }, new List<string>());

        var act = () => DemoDataSeeder.OrderByDependencies(new List<IModule> { orphan });

        act.Should().Throw<InvalidOperationException>().WithMessage("*no such module is registered*");
    }

    [Fact]
    public async Task SeedAsync_RejectsBlankTenantId()
    {
        var seeder = BuildSeeder();
        var act = () => seeder.SeedAsync("", Scenario);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SeedAsync_RejectsNonGuidTenantId()
    {
        var seeder = BuildSeeder();
        var act = () => seeder.SeedAsync("not-a-guid", Scenario);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*TenantId must be a GUID*");
    }

    // --- Helpers ------------------------------------------------------------------

    private static DemoDataSeeder BuildSeeder(params IModule[] modules)
    {
        // Stable DB name per seeder so both runs inside one test share the marker
        // store (AddDbContext invokes the options lambda per scope — a Guid.NewGuid()
        // inside would generate a fresh DB each scope and break idempotency checks).
        var dbName = $"demo-seed-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddDbContext<DemoSeedMarkerDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();

        return new DemoDataSeeder(
            sp.GetRequiredService<IServiceScopeFactory>(),
            modules,
            NullLogger<DemoDataSeeder>.Instance);
    }

    /// <summary>
    /// Minimal <see cref="IModule"/> test double — records each
    /// <see cref="SeedDemoDataAsync"/> invocation into a shared list so tests can
    /// assert ordering and call counts without reflection.
    /// </summary>
    private sealed class FakeModule(
        string name,
        IReadOnlyList<string> dependencies,
        List<string> callLog,
        bool throwOnSeed = false) : IModule
    {
        public string Name => name;
        public string DisplayName => name;
        public string Version => "1.0.0";
        public IReadOnlyList<string> Dependencies => dependencies;

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
        public void ConfigureEventHandlers(IServiceCollection services) { }
        public void ConfigureJobs(IJobScheduler scheduler) { }
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct) => Task.FromResult(HealthCheckResult.Healthy());
        public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct) => Task.CompletedTask;
        public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct) => Task.CompletedTask;
        public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct) => Task.CompletedTask;

        public Task SeedDemoDataAsync(TenantDemoSeedContext context, CancellationToken ct)
        {
            callLog.Add(name);
            if (throwOnSeed)
                throw new InvalidOperationException($"fake module '{name}' is broken on purpose");
            return Task.CompletedTask;
        }
    }
}
