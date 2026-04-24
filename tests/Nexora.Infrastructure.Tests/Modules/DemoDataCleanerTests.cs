using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Modules;
using Nexora.Infrastructure.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Domain.Events;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Modules;

/// <summary>
/// T-009: orchestrator-level unit tests for <see cref="DemoDataCleaner"/>.
/// Mirrors the <c>DemoDataSeederTests</c> shape so the seed/clean pair stay
/// symmetric. Separate integration coverage (schema drop against a real
/// Postgres container) lives in the T-009 follow-up test fixture.
/// </summary>
public sealed class DemoDataCleanerTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string Scenario = "general";

    [Fact]
    public async Task CleanAsync_WhenModulesHaveDependencies_RunsInReverseTopologicalOrder()
    {
        var seedLog = new List<string>();
        var cleanLog = new List<string>();
        var identity = new FakeModule("identity", Array.Empty<string>(), seedLog, cleanLog);
        var contacts = new FakeModule("contacts", new[] { "identity" }, seedLog, cleanLog);
        var crm = new FakeModule("crm", new[] { "identity", "contacts" }, seedLog, cleanLog);

        var (seeder, cleaner) = BuildPair(crm, contacts, identity);

        // Seed first so the cleaner has markers to remove.
        await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        seedLog.Clear();

        var result = await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        cleanLog.Should().ContainInOrder("crm", "contacts", "identity");
        result.Modules.Should().OnlyContain(o => o.Status == DemoCleanStatus.Cleaned);
    }

    [Fact]
    public async Task CleanAsync_WithoutPriorSeed_ReportsNothingToCleanForNoOpModules()
    {
        // NoOpCleanModule truly does NOT override CleanDemoDataAsync — the
        // C# default interface method resolves to IModule's Task.CompletedTask,
        // which IsCleanDefaultNoOp detects via the interface-map scan. Absent
        // a marker row, the orchestrator reports NothingToClean.
        var (_, cleaner) = BuildPair(new NoOpCleanModule("crm"));

        var result = await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        result.Modules.Single().Status.Should().Be(DemoCleanStatus.NothingToClean);
    }

    [Fact]
    public async Task CleanAsync_WithPriorSeed_RemovesMarkerSoReSeedIsNotShortCircuited()
    {
        var seedLog = new List<string>();
        var cleanLog = new List<string>();
        var module = new FakeModule("contacts", Array.Empty<string>(), seedLog, cleanLog);
        var (seeder, cleaner) = BuildPair(module);

        await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        // After clean, re-seeding must run the module again rather than
        // short-circuiting on a stale Seeded marker.
        seedLog.Clear();
        var reseeded = await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        reseeded.Modules.Single().Status.Should().Be(DemoSeedStatus.Seeded,
            "the cleaner must remove the marker row so a subsequent seed is not a no-op.");
        seedLog.Should().ContainSingle().Which.Should().Be("contacts");
    }

    [Fact]
    public async Task CleanAsync_ModuleThrows_DoesNotBlockSiblingsAndMarksFailed()
    {
        var seedLog = new List<string>();
        var cleanLog = new List<string>();
        var broken = new FakeModule("contacts", Array.Empty<string>(), seedLog, cleanLog, throwOnClean: true);
        var healthy = new FakeModule("documents", Array.Empty<string>(), seedLog, cleanLog);
        var (seeder, cleaner) = BuildPair(broken, healthy);

        await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        var result = await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        result.Modules.Single(m => m.ModuleName == "contacts").Status.Should().Be(DemoCleanStatus.Failed);
        result.Modules.Single(m => m.ModuleName == "documents").Status.Should().Be(DemoCleanStatus.Cleaned,
            "a broken sibling's Clean must not block the rest of the pipeline — same policy as the seeder.");
    }

    [Fact]
    public async Task CleanAsync_ModuleThrowsInternalCancellation_DoesNotBlockSiblings()
    {
        // A module that throws OperationCanceledException from its OWN
        // internal token (not the caller's ct) must be treated as a regular
        // module failure — marked Failed, siblings still run. This test
        // protects the `catch (OperationCanceledException) ... if (ct.IsCancellationRequested) throw;`
        // filter in DemoDataCleaner.CleanModuleAsync from silently aborting
        // the whole pipeline on an internal cancellation.
        var seedLog = new List<string>();
        var cleanLog = new List<string>();
        var broken = new FakeModule(
            "contacts", Array.Empty<string>(), seedLog, cleanLog,
            throwInternalCancellation: true);
        var healthy = new FakeModule("documents", Array.Empty<string>(), seedLog, cleanLog);
        var (seeder, cleaner) = BuildPair(broken, healthy);

        await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        // Caller's ct is NOT cancelled — only the module's internal token is.
        var result = await cleaner.CleanAsync(_tenantId.ToString(), Scenario, CancellationToken.None);

        result.Modules.Single(m => m.ModuleName == "contacts").Status.Should().Be(
            DemoCleanStatus.Failed,
            "internal module cancellation is a module failure, not a caller abort.");
        result.Modules.Single(m => m.ModuleName == "documents").Status.Should().Be(
            DemoCleanStatus.Cleaned,
            "sibling modules must still run when one module cancels internally.");
    }

    [Fact]
    public async Task CleanAsync_Idempotent_SecondRunReportsNothingToClean()
    {
        var module = new FakeModule("contacts", Array.Empty<string>(), new List<string>(), new List<string>());
        var (seeder, cleaner) = BuildPair(module);

        await seeder.SeedAsync(_tenantId.ToString(), Scenario);
        await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        // Second clean against an already-clean tenant.
        var second = await cleaner.CleanAsync(_tenantId.ToString(), Scenario);

        // Module has overridden Clean, so it's not "NoOp"; but marker is
        // already gone. Current impl invokes CleanDemoDataAsync a second
        // time (safe: modules are documented to be idempotent) and then
        // finds no marker to delete. Reports Cleaned with no side-effects.
        second.Modules.Single().Status.Should().Be(DemoCleanStatus.Cleaned);
    }

    [Fact]
    public async Task CleanAsync_InvalidTenantId_Throws()
    {
        var (_, cleaner) = BuildPair(new FakeModule("x", Array.Empty<string>(), new List<string>(), new List<string>()));
        var act = async () => await cleaner.CleanAsync("not-a-guid", Scenario);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // --- Helpers -----------------------------------------------------------------

    private static (DemoDataSeeder seeder, DemoDataCleaner cleaner) BuildPair(params IModule[] modules)
    {
        // Share a single InMemory DB across seeder + cleaner so markers written
        // by seed are visible to clean.
        var dbName = $"demo-clean-{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
        services.AddDbContext<DemoSeedMarkerDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var seeder = new DemoDataSeeder(scopeFactory, modules, NullLogger<DemoDataSeeder>.Instance);

        // Event bus is only touched by DropTenantAsync, which this file does
        // NOT cover (schema-drop semantics need a relational provider — lives
        // in the integration fixture). Substituted to satisfy the ctor.
        var eventBus = Substitute.For<IEventBus>();
        var cleaner = new DemoDataCleaner(scopeFactory, modules, eventBus, NullLogger<DemoDataCleaner>.Instance);

        return (seeder, cleaner);
    }

    /// <summary>
    /// Minimal <see cref="IModule"/> test double with both Seed + Clean call
    /// logs. When <paramref name="defaultClean"/> is true, the module inherits
    /// the default no-op <see cref="IModule.CleanDemoDataAsync"/> via the
    /// C# default interface method — lets the tests verify the NothingToClean
    /// branch that depends on interface-map reflection.
    /// </summary>
    private sealed class FakeModule : IModule
    {
        private readonly IReadOnlyList<string> _dependencies;
        private readonly List<string> _seedLog;
        private readonly List<string> _cleanLog;
        private readonly bool _throwOnClean;
        private readonly bool _throwInternalCancellation;

        public FakeModule(
            string name,
            IReadOnlyList<string> dependencies,
            List<string> seedLog,
            List<string> cleanLog,
            bool throwOnClean = false,
            bool throwInternalCancellation = false)
        {
            Name = name;
            _dependencies = dependencies;
            _seedLog = seedLog;
            _cleanLog = cleanLog;
            _throwOnClean = throwOnClean;
            _throwInternalCancellation = throwInternalCancellation;
        }

        public string Name { get; }
        public string DisplayName => Name;
        public string Version => "1.0.0";
        public IReadOnlyList<string> Dependencies => _dependencies;

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
            _seedLog.Add(Name);
            return Task.CompletedTask;
        }

        public Task CleanDemoDataAsync(TenantDemoSeedContext context, CancellationToken ct)
        {
            _cleanLog.Add(Name);
            if (_throwOnClean)
                throw new InvalidOperationException($"fake module '{Name}' is broken on clean");
            if (_throwInternalCancellation)
            {
                // Simulate a module's OWN timeout / internal cancellation —
                // NOT the caller's ct. The token passed to the exception is
                // a cancelled one the module created locally; the outer ct
                // is still alive (caller has not cancelled).
                using var internalCts = new CancellationTokenSource();
                internalCts.Cancel();
                throw new OperationCanceledException(internalCts.Token);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Second test double that deliberately inherits the default no-op
    /// <see cref="IModule.CleanDemoDataAsync"/> via the C# default interface
    /// method — used to exercise the <c>NothingToClean</c> / <c>NoOp</c>
    /// branches of <see cref="DemoDataCleaner"/> that depend on interface-map
    /// reflection detecting an un-overridden method. This class overrides
    /// only <see cref="IModule.SeedDemoDataAsync"/>; leaving
    /// <c>CleanDemoDataAsync</c> untouched is load-bearing for the tests.
    /// </summary>
    private sealed class NoOpCleanModule : IModule
    {
        public NoOpCleanModule(string name) { Name = name; }

        public string Name { get; }
        public string DisplayName => Name;
        public string Version => "1.0.0";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
        public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
        public void ConfigureEventHandlers(IServiceCollection services) { }
        public void ConfigureJobs(IJobScheduler scheduler) { }
        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken ct) => Task.FromResult(HealthCheckResult.Healthy());
        public Task OnStartupAsync(IPermissionRegistry registry, CancellationToken ct) => Task.CompletedTask;
        public Task OnInstallAsync(TenantInstallContext context, CancellationToken ct) => Task.CompletedTask;
        public Task OnUninstallAsync(TenantInstallContext context, CancellationToken ct) => Task.CompletedTask;

        public Task SeedDemoDataAsync(TenantDemoSeedContext context, CancellationToken ct) => Task.CompletedTask;
        // CleanDemoDataAsync intentionally NOT overridden — default interface impl kicks in.
    }
}
