using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.Host.Cli;
using Nexora.SharedKernel.Abstractions.Modules;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Cli;

/// <summary>
/// T-006: unit tests for the <c>demo:load</c> CLI command — parser + runner
/// behaviour. Integration-style tests live in T-006's manual QA plan (they
/// require a real Postgres schema and are exercised via
/// <c>docker compose run nexora-api demo:load ...</c>).
/// </summary>
public sealed class DemoLoadCommandTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string Scenario = "general";

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
    public async Task RunAsync_DryRun_DoesNotInvokeSeeder_AndReturnsZero()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}", "--dry-run"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _) => Task.FromResult(true),
            console);

        exit.Should().Be(0);
        console.Lines.Should().Contain(l => l.Contains("[dry-run]") && l.Contains("no changes made"));
        await seeder.DidNotReceive().SeedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_MissingTenantSchema_ReturnsUsageError_WithPointer()
    {
        var seeder = Substitute.For<IDemoDataSeeder>();
        using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _) => Task.FromResult(false),
            console);

        exit.Should().Be(CliDispatcher.UsageError);
        console.Lines.Should().Contain(l => l.Contains("does not exist"));
        console.Lines.Should().Contain(l => l.Contains("Identity admin API"));
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

        using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _) => Task.FromResult(true),
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

        using var host = BuildHostWith(seeder);
        var console = new RecordingConsole();
        var opts = DemoLoadCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}"
        });

        var exit = await DemoLoadCommand.RunAsync(
            opts,
            hostFactory: () => host,
            tenantSchemaProbe: (_, _) => Task.FromResult(true),
            console);

        exit.Should().Be(CliDispatcher.PartialFailure);
        console.Lines.Should().Contain(l => l.Contains("contacts") && l.Contains("FAILED") && l.Contains("db connection refused"));
    }

    // --- Helpers ----------------------------------------------------------------

    /// <summary>
    /// Returns a freshly-built <see cref="IHost"/> with <paramref name="seeder"/>
    /// registered as a singleton. Caller MUST dispose (use <c>using</c>)
    /// — leaking the host leaks the DI container, the logger providers, and any
    /// transitively-registered hosted services. <c>IHost</c>'s interface form is
    /// <see cref="IDisposable"/>; the concrete impl also surfaces
    /// <see cref="IAsyncDisposable"/> but the interface drives the <c>using</c>.
    /// </summary>
    private static IHost BuildHostWith(IDemoDataSeeder seeder)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(seeder);
        return builder.Build();
    }

    private sealed class RecordingConsole : IConsole
    {
        public List<string> Lines { get; } = new();
        public void WriteLine(string line) => Lines.Add(line);
    }
}
