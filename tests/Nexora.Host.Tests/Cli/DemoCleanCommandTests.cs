using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.Host.Cli;
using Nexora.SharedKernel.Abstractions.Modules;
using NSubstitute;

namespace Nexora.Host.Tests.Cli;

/// <summary>
/// T-009: unit coverage for the <c>demo:clean</c> CLI verb — parser, dispatcher
/// fall-through, dry-run, normal-clean, drop-tenant confirmation gate, and
/// partial-failure mapping. Mirrors the <c>DemoLoadCommandTests</c> shape so
/// both CLI test files keep one test layout.
/// </summary>
public sealed class DemoCleanCommandTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string Scenario = "general";

    // Restore-after-test culture discipline: assert EN during the test so
    // lockey-token `Contains(...)` assertions resolve to known English text,
    // then restore the original culture in Dispose so the mutation does not
    // leak to sibling test classes or a subsequent test run in the same
    // process (xUnit does NOT isolate process-wide culture across test
    // classes; a static ctor here would poison the whole assembly).
    private readonly System.Globalization.CultureInfo _originalCulture;
    private readonly System.Globalization.CultureInfo? _originalDefault;

    public DemoCleanCommandTests()
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
    public void TryDispatch_DemoCleanVerb_RoutesToDemoCleanCommand()
    {
        // Unknown-scenario args force a usage-error exit (1) without building
        // a host — the cheapest way to prove the dispatcher routed to the
        // correct command.
        var dispatched = CliDispatcher.TryDispatch(
            new[] { "demo:clean", "--tenant=not-a-guid" }, out var exitCode);

        dispatched.Should().BeTrue("demo:clean is a recognised verb and must not fall through to the web host");
        exitCode.Should().Be(CliDispatcher.UsageError);
    }

    [Fact]
    public void ParseArgs_EqualsForm_AndSpaceForm_BothWork()
    {
        var a = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--scenario=general", "--dry-run" });
        var b = DemoCleanCommand.ParseArgs(new[] { "--tenant", _tenantId.ToString(), "--scenario", "general", "--dry-run" });

        a.TenantId.Should().Be(_tenantId);
        a.Scenario.Should().Be("general");
        a.DryRun.Should().BeTrue();
        a.DropTenant.Should().BeFalse();
        a.Yes.Should().BeFalse();

        b.Should().BeEquivalentTo(a);
    }

    [Fact]
    public void ParseArgs_DropTenantAndYes_AreBothSet()
    {
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--drop-tenant", "--yes" });

        opts.DropTenant.Should().BeTrue();
        opts.Yes.Should().BeTrue();
        opts.Scenario.Should().BeNull("--drop-tenant bypasses scenario — the whole schema is gone anyway");
    }

    [Fact]
    public async Task RunAsync_MissingTenantFlag_ReturnsUsageError()
    {
        var opts = DemoCleanCommand.ParseArgs(new[] { "--scenario=general" });
        var console = new RecordingConsole();

        var exit = await DemoCleanCommand.RunAsync(
            opts,
            hostFactory: () => throw new InvalidOperationException("host must not be built on usage error"),
            console);

        exit.Should().Be(CliDispatcher.UsageError);
        console.Lines.Should().Contain(l => l.Contains("--tenant"));
    }

    [Fact]
    public async Task RunAsync_DropTenantWithoutYes_ReturnsUsageError()
    {
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--drop-tenant" });
        var console = new RecordingConsole();

        var exit = await DemoCleanCommand.RunAsync(
            opts,
            hostFactory: () => throw new InvalidOperationException("host must not be built on usage error"),
            console);

        exit.Should().Be(CliDispatcher.UsageError);
        console.Lines.Should().Contain(l => l.Contains("--yes"),
            "the --drop-tenant gate must explicitly require --yes to prevent accidental destruction.");
    }

    [Fact]
    public async Task RunAsync_WithDryRun_SkipsCleanerAndReturnsZero()
    {
        var cleaner = Substitute.For<IDemoDataCleaner>();
        await using var host = BuildHostWith(cleaner);
        var console = new RecordingConsole();
        var opts = DemoCleanCommand.ParseArgs(new[]
        {
            $"--tenant={_tenantId}", $"--scenario={Scenario}", "--dry-run"
        });

        var exit = await DemoCleanCommand.RunAsync(opts, hostFactory: () => host, console);

        exit.Should().Be(0);
        console.Lines.Should().Contain(l => l.Contains("[dry-run]"));
        await cleaner.DidNotReceive().CleanAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_AllModulesCleaned_ReturnsZero()
    {
        var cleaner = Substitute.For<IDemoDataCleaner>();
        cleaner.CleanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoCleanRunResult(_tenantId.ToString(), Scenario, new List<DemoCleanModuleOutcome>
            {
                new("contacts", DemoCleanStatus.Cleaned),
                new("identity", DemoCleanStatus.NoOp)
            }));

        await using var host = BuildHostWith(cleaner);
        var console = new RecordingConsole();
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", $"--scenario={Scenario}" });

        var exit = await DemoCleanCommand.RunAsync(opts, hostFactory: () => host, console);

        exit.Should().Be(0);
        console.Lines.Should().Contain(l => l.Contains("contacts") && l.Contains("cleaned"));
    }

    [Fact]
    public async Task RunAsync_AnyModuleFailed_ReturnsPartialFailure()
    {
        var cleaner = Substitute.For<IDemoDataCleaner>();
        cleaner.CleanAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoCleanRunResult(_tenantId.ToString(), Scenario, new List<DemoCleanModuleOutcome>
            {
                new("contacts", DemoCleanStatus.Cleaned),
                new("identity", DemoCleanStatus.Failed, "FK violation")
            }));

        await using var host = BuildHostWith(cleaner);
        var console = new RecordingConsole();
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", $"--scenario={Scenario}" });

        var exit = await DemoCleanCommand.RunAsync(opts, hostFactory: () => host, console);

        exit.Should().Be(CliDispatcher.PartialFailure);
        console.Lines.Should().Contain(l => l.Contains("identity") && l.Contains("FAILED"));
    }

    [Fact]
    public async Task RunAsync_DropTenantWithYes_InvokesCleanerAndReturnsZero()
    {
        var cleaner = Substitute.For<IDemoDataCleaner>();
        cleaner.DropTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoDropTenantResult(_tenantId.ToString(), SchemaDropped: true));

        await using var host = BuildHostWith(cleaner);
        var console = new RecordingConsole();
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--drop-tenant", "--yes" });

        var exit = await DemoCleanCommand.RunAsync(opts, hostFactory: () => host, console);

        exit.Should().Be(0);
        await cleaner.Received(1).DropTenantAsync(_tenantId.ToString(), Arg.Any<CancellationToken>());
        await cleaner.DidNotReceive().CleanAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_DropTenantSchemaDroppedButPublishFailed_ReturnsPartialFailure()
    {
        var cleaner = Substitute.For<IDemoDataCleaner>();
        cleaner.DropTenantAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DemoDropTenantResult(_tenantId.ToString(), SchemaDropped: true,
                ErrorMessage: "Schema dropped; event publish failed: dapr down"));

        await using var host = BuildHostWith(cleaner);
        var console = new RecordingConsole();
        var opts = DemoCleanCommand.ParseArgs(new[] { $"--tenant={_tenantId}", "--drop-tenant", "--yes" });

        var exit = await DemoCleanCommand.RunAsync(opts, hostFactory: () => host, console);

        exit.Should().Be(CliDispatcher.PartialFailure);
        // --drop-tenant outcome messages now stream on stderr (Finding #25),
        // so the error-message surface lives on ErrorLines not Lines.
        console.ErrorLines.Should().Contain(l => l.Contains("dapr down"));
    }

    // --- Helpers ------------------------------------------------------------

    private static AsyncDisposableHost BuildHostWith(IDemoDataCleaner cleaner)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Configuration.Sources.Clear();
        builder.Services.AddSingleton(cleaner);
        return new AsyncDisposableHost(builder.Build());
    }

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
