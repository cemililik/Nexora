using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var parsed = ParseArgs(args);
        return RunAsync(parsed, DemoLoadHostFactory.Build, tenantSchemaProbe: null)
            .GetAwaiter().GetResult();
    }

    internal static async Task<int> RunAsync(
        DemoLoadOptions options,
        Func<IHost> hostFactory,
        Func<IServiceProvider, Guid, Task<bool>>? tenantSchemaProbe,
        IConsole? console = null)
    {
        console ??= SystemConsole.Instance;

        if (!options.IsValid(out var usageError))
        {
            console.WriteLine(usageError);
            console.WriteLine("Usage: demo:load --tenant=<guid> --scenario=<name> [--dry-run]");
            return CliDispatcher.UsageError;
        }

        using var host = hostFactory();
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Tenant schema precheck — a CLI run against a tenant that was never
        // provisioned should fail fast with a clear message instead of exploding
        // inside the seeder's first DbContext resolution. Tests inject a probe;
        // production uses the default Postgres pg_namespace lookup.
        var probe = tenantSchemaProbe ?? DefaultTenantSchemaProbeAsync;
        if (!await probe(services, options.TenantId!.Value))
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
            options.TenantId!.Value.ToString(), options.Scenario!);

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

            if (TryParseFlag(a, "--tenant", out var tenantValue) ||
                (a == "--tenant" && i + 1 < args.Length && !(tenantValue = args[++i]).StartsWith("--")))
            {
                if (Guid.TryParse(tenantValue, out var guid))
                    tenantId = guid;
                else
                    unknown.Add($"invalid tenant GUID: '{tenantValue}'");
                continue;
            }

            if (TryParseFlag(a, "--scenario", out var scenarioValue) ||
                (a == "--scenario" && i + 1 < args.Length && !(scenarioValue = args[++i]).StartsWith("--")))
            {
                scenario = scenarioValue;
                continue;
            }

            unknown.Add(a);
        }

        return new DemoLoadOptions(tenantId, scenario, dryRun, unknown);
    }

    private static bool TryParseFlag(string arg, string name, out string value)
    {
        value = "";
        if (!arg.StartsWith(name + "=", StringComparison.Ordinal)) return false;
        value = arg[(name.Length + 1)..];
        return true;
    }

    private static async Task<bool> DefaultTenantSchemaProbeAsync(IServiceProvider services, Guid tenantId)
    {
        // Uses the first DbContext in the DI container to run a catalog-level check
        // (pg_namespace). We avoid binding to any specific module's DbContext.
        var connection = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            .GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connection)) return false;

        await using var conn = new Npgsql.NpgsqlConnection(connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM pg_namespace WHERE nspname = @name";
        cmd.Parameters.AddWithValue("name", $"tenant_{tenantId}");
        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
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
