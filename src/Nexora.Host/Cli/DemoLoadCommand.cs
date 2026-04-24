using System.Data.Common;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.Infrastructure.Configuration;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Host.Cli;

/// <summary>
/// <c>nexora demo:load</c> command (T-006). Parses CLI flags, builds a minimal host
/// (no web-server start), checks the tenant schema exists, and either prints the
/// plan (`--dry-run`) or runs <see cref="IDemoDataSeeder"/> and surfaces a
/// per-module summary with a meaningful exit code.
/// </summary>
public static class DemoLoadCommand
{
    /// <summary>
    /// Entry point called by <see cref="CliDispatcher"/> — pass all args AFTER the
    /// <c>demo:load</c> verb.
    /// </summary>
    public static int Run(ReadOnlySpan<string> args)
    {
        // Capture args into a string[] before crossing the async boundary —
        // ReadOnlySpan<string> cannot escape into a Task closure.
        var argv = args.ToArray();
        var parsed = ParseArgs(argv);
        try
        {
            return RunAsync(parsed, () => DemoLoadHostFactory.Build(argv),
                    tenantSchemaProbe: null)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        // CLI is the OUTERMOST process boundary — by design (ADR's
        // "no catch(Exception) in module code" rule applies inside modules,
        // not at the process entry point). We narrow to the families the
        // demo:load pipeline actually produces and let anything else
        // (StackOverflowException, OutOfMemoryException, AccessViolation, etc.)
        // bubble so the process crashes loudly instead of swallowing fatal
        // CLR conditions. Explicit families:
        //   ArgumentException / InvalidOperationException → usage / config
        //   OperationCanceledException                    → caller cancelled
        //   DbException (incl. Npgsql)                    → infrastructure
        //   HttpRequestException / SocketException        → infrastructure
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(CliLocalization.T("lockey_cli_demoload_cancelled"));
            return CliDispatcher.UsageError;
        }
        catch (ArgumentException ex)
        {
            WriteFailure(ex);
            return CliDispatcher.UsageError;
        }
        catch (InvalidOperationException ex)
        {
            WriteFailure(ex);
            return CliDispatcher.UsageError;
        }
        catch (DbException ex)
        {
            WriteFailure(ex);
            return CliDispatcher.PartialFailure;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            WriteFailure(ex);
            return CliDispatcher.PartialFailure;
        }
        catch (SocketException ex)
        {
            WriteFailure(ex);
            return CliDispatcher.PartialFailure;
        }
    }

    private static void WriteFailure(Exception ex)
    {
        Console.Error.WriteLine($"demo:load failed: {ex.Message}");
        Console.Error.WriteLine($"  ({ex.GetType().Name})");
    }

    internal static async Task<int> RunAsync(
        DemoLoadOptions options,
        Func<IHost> hostFactory,
        Func<IServiceProvider, Guid, CancellationToken, Task<bool>>? tenantSchemaProbe,
        IConsole? console = null,
        CancellationToken ct = default)
    {
        console ??= SystemConsole.Instance;

        if (!options.IsValid(out var usageError))
        {
            console.WriteLine(usageError);
            console.WriteLine("Usage: demo:load --tenant=<guid> --scenario=<name> [--dry-run]");
            return CliDispatcher.UsageError;
        }

        // Async disposal honours hosted services / DbContexts that implement
        // IAsyncDisposable.
        await using var host = (IAsyncDisposable)hostFactory();
        var typedHost = (IHost)host;
        await using var scope = typedHost.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Tenant schema precheck — a CLI run against a tenant that was never
        // provisioned should fail fast with a clear message instead of exploding
        // inside the seeder's first DbContext resolution. Tests inject a probe;
        // production uses the default Postgres pg_namespace lookup.
        var probe = tenantSchemaProbe ?? DefaultTenantSchemaProbeAsync;
        if (!await probe(services, options.TenantId!.Value, ct))
        {
            console.WriteLine($"Tenant schema 'tenant_{options.TenantId.Value}' does not exist.");
            console.WriteLine("Provision the tenant first via the Identity admin API; demo:load does not auto-provision.");
            return CliDispatcher.UsageError;
        }

        if (options.DryRun)
        {
            console.WriteLine($"[dry-run] demo:load tenant={options.TenantId} scenario={options.Scenario}");
            console.WriteLine($"[dry-run] would iterate IModule.SeedDemoDataAsync over {CountModules(services)} modules and write idempotency markers on success.");
            console.WriteLine("[dry-run] no changes made.");
            return 0;
        }

        var seeder = services.GetRequiredService<IDemoDataSeeder>();
        var result = await seeder.SeedAsync(
            options.TenantId!.Value.ToString(), options.Scenario!, ct);

        console.WriteLine($"demo:load completed for tenant {options.TenantId} scenario {options.Scenario}:");
        foreach (var outcome in result.Modules)
        {
            var label = outcome.Status switch
            {
                DemoSeedStatus.Seeded => "seeded",
                DemoSeedStatus.AlreadySeeded => "already-seeded",
                DemoSeedStatus.NoOp => "no-op (no demo content)",
                DemoSeedStatus.Failed => $"FAILED — {outcome.ErrorMessage}",
                _ => outcome.Status.ToString()
            };
            console.WriteLine($"  - {outcome.ModuleName}: {label}");
        }

        return result.Modules.Any(m => m.Status == DemoSeedStatus.Failed)
            ? CliDispatcher.PartialFailure
            : 0;
    }

    /// <summary>
    /// Parses supported flags. Accepts both <c>--key=value</c> and <c>--key value</c>
    /// forms; unknown flags are collected into <see cref="DemoLoadOptions.UnknownArgs"/>
    /// so the caller can surface them in the usage error message.
    /// </summary>
    internal static DemoLoadOptions ParseArgs(ReadOnlySpan<string> args)
    {
        Guid? tenantId = null;
        string? scenario = null;
        bool dryRun = false;
        var unknown = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--dry-run") { dryRun = true; continue; }

            if (TryConsumeFlag(args, ref i, a, "--tenant", out var tenantValue))
            {
                if (Guid.TryParse(tenantValue, out var guid))
                    tenantId = guid;
                else
                    unknown.Add($"invalid tenant GUID: '{tenantValue}'");
                continue;
            }

            if (TryConsumeFlag(args, ref i, a, "--scenario", out var scenarioValue))
            {
                scenario = scenarioValue;
                continue;
            }

            unknown.Add(a);
        }

        return new DemoLoadOptions(tenantId, scenario, dryRun, unknown);
    }

    /// <summary>
    /// Tries to extract a value for <paramref name="name"/> from either the
    /// <c>--name=value</c> equals form or the <c>--name value</c> space form.
    /// CRITICAL: only advances the index <paramref name="i"/> when the space
    /// form successfully consumes a non-flag value. The previous version
    /// incremented <paramref name="i"/> as a side-effect of the boolean
    /// short-circuit even when the next token started with <c>--</c>, which
    /// silently ate the following flag and produced a trailing UnknownArg
    /// for the eaten value.
    /// </summary>
    private static bool TryConsumeFlag(
        ReadOnlySpan<string> args, ref int i, string current, string name, out string value)
    {
        // Equals form first — pure parse, no index movement.
        if (TryParseFlag(current, name, out value)) return true;

        // Space form — peek args[i+1] WITHOUT mutating i; only advance if the
        // peek looks like a real value.
        if (current == name && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[i + 1];
            i++;
            return true;
        }

        value = "";
        return false;
    }

    private static bool TryParseFlag(string arg, string name, out string value)
    {
        value = "";
        if (!arg.StartsWith(name + "=", StringComparison.Ordinal)) return false;
        value = arg[(name.Length + 1)..];
        return true;
    }

    private static async Task<bool> DefaultTenantSchemaProbeAsync(
        IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        // Use TenantConfigDbContext's underlying connection so we honour Npgsql
        // pooling, retry policy, and the application's connection-string
        // resolution (tenant accessor, secrets) instead of new-ing a raw
        // NpgsqlConnection out of IConfiguration. The probe is a catalog-level
        // check against pg_namespace; the DbContext's model is irrelevant here,
        // we just borrow the connection.
        //
        // TenantConfigDbContext is registered AddDbContext (scoped) and its
        // OnModelCreating reads ITenantContextAccessor.Current.SchemaName — set
        // a benign tenant context before resolution so model-build does not
        // throw, even though we never query a mapped table.
        var accessor = services.GetRequiredService<
            Nexora.SharedKernel.Abstractions.MultiTenancy.ITenantContextAccessor>();
        accessor.SetTenant(tenantId.ToString());

        var dbContext = services.GetRequiredService<TenantConfigDbContext>();
        var conn = dbContext.Database.GetDbConnection();
        var ownedOpen = conn.State != System.Data.ConnectionState.Open;
        if (ownedOpen)
            await conn.OpenAsync(ct);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM pg_namespace WHERE nspname = @name";
            var p = cmd.CreateParameter();
            p.ParameterName = "@name";
            p.Value = $"tenant_{tenantId}";
            cmd.Parameters.Add(p);
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is not null;
        }
        finally
        {
            // Only close what we opened; if EF was already managing the
            // connection, leave it alone so its lifecycle stays consistent.
            if (ownedOpen)
                await conn.CloseAsync();
        }
    }

    private static int CountModules(IServiceProvider services)
        => services.GetServices<IModule>().Count();
}

/// <summary>Parsed options for <c>demo:load</c>.</summary>
internal sealed record DemoLoadOptions(
    Guid? TenantId,
    string? Scenario,
    bool DryRun,
    IReadOnlyList<string> UnknownArgs)
{
    public bool IsValid(out string error)
    {
        if (UnknownArgs.Count > 0)
        {
            error = "Unrecognised or invalid argument(s): " + string.Join(", ", UnknownArgs);
            return false;
        }
        if (TenantId is null) { error = "Missing required flag: --tenant=<guid>"; return false; }
        if (string.IsNullOrWhiteSpace(Scenario)) { error = "Missing required flag: --scenario=<name>"; return false; }
        error = "";
        return true;
    }
}

/// <summary>
/// Thin abstraction so the command is testable without spinning up a full host.
/// Tests inject an in-process <see cref="IServiceProvider"/>; production uses
/// <see cref="DemoLoadHostFactory"/>.
/// </summary>
internal interface IConsole
{
    void WriteLine(string line);
}

internal sealed class SystemConsole : IConsole
{
    public static readonly SystemConsole Instance = new();
    public void WriteLine(string line) => Console.WriteLine(line);
}
