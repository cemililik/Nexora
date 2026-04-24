using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.Host.Cli;
using Nexora.SharedKernel.Abstractions.Modules;
using NSubstitute;

namespace Nexora.Host.Tests.Cli;

/// <summary>
/// T-006: unit tests for the <c>demo:load</c> CLI command — parser + runner
/// behaviour. Integration-style tests live in T-006's manual QA plan (they
/// require a real Postgres schema and are exercised via
/// <c>docker compose run nexora-api demo:load ...</c>).
/// </summary>
public sealed class DemoLoadCommandTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string Scenario = "general";

    // Restore-after-test culture discipline (see DemoCleanCommandTests for
    // the same pattern + rationale). Static ctors would mutate process-wide
    // state across every test class — unacceptable for xUnit shared
    // collections. Instance ctor + Dispose restores.
    private readonly System.Globalization.CultureInfo _originalCulture;
    private readonly System.Globalization.CultureInfo? _originalDefault;

    public DemoLoadCommandTests()
    {
        _originalCulture = System.Globalization.CultureInfo.CurrentUICulture;
        _originalDefault = System.Globalization.CultureInfo.DefaultThreadCurrentUICulture;
        System.Threading.Thread.CurrentThread.CurrentUICulture =
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture =
                new System.Globalization.CultureInfo("en");
    }

    public void Dispose()
    {
        System.Threading.Thread.CurrentThread.CurrentUICulture = _originalCulture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = _originalDefault;
    }

    [Fact]
    public void TryDispatch_NotACliVerb_FallsThroughToWebHost()
    {
        var dispatched = CliDispatcher.TryDispatch(
            ["--urls", "http://0.0.0.0:5000"], out var exitCode);

        dispatched.Should().BeFalse("unknown first arg must fall through to the web host");
        exitCode.Should().Be(0);
    }

    [Fact]
    public void TryDispatch_EmptyArgs_FallsThrough()
    {
        var dispatched = CliDispatcher.TryDispatch([], out var exitCode);
        dispatched.Should().BeFalse();
        exitCode.Should().Be(0);
    }

    [Fact]
    public void ParseArgs_EqualsForm_AndSpaceForm_BothWork()
    {
        var a = DemoLoadCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--scenario=general", "--dry-run" });
        var b = DemoLoadCommand.ParseArgs(new[] { "--tenant", _tenantId.ToString(), "--scenario", "general", "--dry-run" });

        a.TenantId.Should().Be(_tenantId);
        a.Scenario.Should().Be("general");
        a.DryRun.Should().BeTrue();
        a.UnknownArgs.Should().BeEmpty();

        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public void ParseArgs_InvalidTenantGuid_CollectedAsUnknown()
    {
        var opts = DemoLoadCommand.ParseArgs(new[] { "--tenant=not-a-guid", "--scenario=x" });

        opts.TenantId.Should().BeNull();
        opts.UnknownArgs.Should().ContainSingle(m => m.Contains("not-a-guid"));
    }

    [Fact]
    public async Task RunAsync_MissingTenantFlag_ReturnsUsageError()
    {
        var opts = DemoLoadCommand.ParseArgs(new[] { "--scenario=general" });
        var console = new RecordingConsole();

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => throw new InvalidOperationException("host must not be built on usage error"),
            tenantSchemaProbe: null,
            console);

        exit.Should().Be(CliDispatcher.UsageError);
        console.Lines.Should().Contain(l => l.Contains("--tenant"));
    }

    [Fact]
    public async Task RunAsync_WithDryRun_SkipsSeederAndReturnsZero()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        await using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}", "--dry-run"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _, _) => Task.FromResult(true),
            console);

        exit.Should().Be(0);
        // Dry-run line check loosened to match localized text — both en and tr
        // bundles include "[dry-run]" but localized "no changes made" varies.
        console.Lines.Should().Contain(l => l.Contains("[dry-run]"));
        await seeder.DidNotReceive().SeedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenTenantSchemaMissing_ReturnsUsageErrorWithPointer()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        await using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _, _) => Task.FromResult(false),
            console);

        exit.Should().Be(CliDispatcher.UsageError);
        // The "schema missing" + "admin API pointer" lines are now localized
        // — match on the lockey-token markers that survive translation
        // (the tenant_<guid> prefix and the API pointer's "admin API" anchor
        // appear in both en and tr bundles).
        console.Lines.Should().Contain(l => l.Contains("tenant_"));
        console.Lines.Should().Contain(l => l.Contains("Identity admin API") || l.Contains("Identity admin API'si"));
        await seeder.DidNotReceive().SeedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AllModulesSeed_ReturnsZero()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        seeder.SeedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoSeedRunResult(_tenantId.ToString(), Scenario, new List<DemoSeedModuleOutcome>
            {
                new("identity", DemoSeedStatus.Seeded),
                new("contacts", DemoSeedStatus.AlreadySeeded)
            }));

        await using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _, _) => Task.FromResult(true),
            console);

        exit.Should().Be(0, "all modules seeded or were already-seeded — no failures.");
        console.Lines.Should().Contain(l => l.Contains("identity") && l.Contains("seeded"));
        console.Lines.Should().Contain(l => l.Contains("contacts") && l.Contains("already-seeded"));
    }

    [Fact]
    public async Task RunAsync_AnyModuleFailed_ReturnsPartialFailure()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        seeder.SeedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoSeedRunResult(_tenantId.ToString(), Scenario, new List<DemoSeedModuleOutcome>
            {
                new("identity", DemoSeedStatus.Seeded),
                new("contacts", DemoSeedStatus.Failed, "db connection refused")
            }));

        await using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _, _) => Task.FromResult(true),
            console);

        exit.Should().Be(CliDispatcher.PartialFailure);
        console.Lines.Should().Contain(l => l.Contains("contacts") && l.Contains("FAILED") && l.Contains("db connection refused"));
    }

    // --- Helpers ----------------------------------------------------------------

    /// <summary>
    /// Returns a freshly-built host with <paramref name="seeder"/> registered
    /// as a singleton. Caller MUST dispose (use <c>await using</c>) — leaking
    /// the host leaks the DI container, the logger providers, and any
    /// transitively-registered hosted services.
    /// <para>
    /// Returns <see cref="AsyncDisposableHost"/> rather than the bare
    /// <see cref="IHost"/> interface because <see cref="IHost"/> only extends
    /// <see cref="IDisposable"/> — <c>await using var x = ...IHost</c> fails
    /// to compile (CS8417). The wrapper exposes both <see cref="IHost"/>
    /// (so it slots into the existing <c>Func&lt;IHost&gt;</c> hostFactory
    /// shape via implicit conversion) and <see cref="IAsyncDisposable"/>
    /// (so <c>await using</c> drains async-disposable resources).
    /// </para>
    /// <para>
    /// Test isolation: passes <see cref="Array.Empty{T}"/> for args AND clears
    /// <c>builder.Configuration.Sources</c> so the host does not inherit
    /// <c>DOTNET_*</c> env vars, <c>appsettings.json</c>, user-secrets, or
    /// the running host's environment-specific config. Each test sees a clean
    /// configuration root and only the seeder we explicitly register.
    /// </para>
    /// </summary>
    private static AsyncDisposableHost BuildHostWith(IDemoDataSeeder seeder)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(seeder);
        return new AsyncDisposableHost(builder.Build());
    }

    /// <summary>
    /// Wrapper that exposes the inner <see cref="IHost"/> as both
    /// <see cref="IHost"/> (forwarded) and <see cref="IAsyncDisposable"/>
    /// (drains the inner's <c>DisposeAsync</c> when available, falling
    /// back to sync <c>Dispose</c>). Lets tests use
    /// <c>await using var host = BuildHostWith(...);</c> without the
    /// CS8417 wall the bare <see cref="IHost"/> interface throws up.
    /// </summary>
    private sealed class AsyncDisposableHost(IHost inner) : IHost, IAsyncDisposable
    {
        public IServiceProvider Services => inner.Services;
        public Task StartAsync(CancellationToken cancellationToken = default) => inner.StartAsync(cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken = default) => inner.StopAsync(cancellationToken);
        public void Dispose() => inner.Dispose();
        public ValueTask DisposeAsync()
        {
            if (inner is IAsyncDisposable iad) return iad.DisposeAsync();
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingConsole : IConsole
    {
        public List<string> Lines { get; } = new();
        public List<string> ErrorLines { get; } = new();
        public void WriteLine(string line) => Lines.Add(line);
        public void WriteErrorLine(string line) => ErrorLines.Add(line);
    }
}
