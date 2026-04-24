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
            return CliDispatcher.Cancelled;
        }
        catch (ArgumentException ex)
        {
            WriteFailure(ex, parsed.Verbose);
            return CliDispatcher.UsageError;
        }
        catch (InvalidOperationException ex)
        {
            WriteFailure(ex, parsed.Verbose);
            return CliDispatcher.UsageError;
        }
        catch (DbException ex)
        {
            WriteFailure(ex, parsed.Verbose);
            return CliDispatcher.PartialFailure;
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            WriteFailure(ex, parsed.Verbose);
            return CliDispatcher.PartialFailure;
        }
        catch (SocketException ex)
        {
            WriteFailure(ex, parsed.Verbose);
            return CliDispatcher.PartialFailure;
        }
    }

    /// <summary>
    /// Surface a CLI failure to stderr without leaking server-supplied
    /// details by default. The default line is generic — only the exception
    /// type name (e.g. <c>NpgsqlException</c>) is printed, NOT the message
    /// (which can contain DB connection details, SQL fragments, or other
    /// internal information). When the operator passes <c>--verbose</c>,
    /// the underlying message is included for debugging.
    /// </summary>
    private static void WriteFailure(Exception ex, bool verbose)
    {
        if (verbose)
        {
            Console.Error.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_failed_verbose_template"),
                ex.Message, ex.GetType().Name));
            return;
        }
        Console.Error.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            CliLocalization.T("lockey_cli_demoload_failed_generic"),
            ex.GetType().Name));
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
            console.WriteLine(CliLocalization.T("lockey_cli_demoload_usage_hint"));
            return CliDispatcher.UsageError;
        }

        // Synchronous `using` for both host and scope. The host is built but
        // never started (DemoLoadHostFactory.Build does not call StartAsync),
        // so there are no hosted services to drain — synchronous IDisposable
        // is sufficient. Avoids the (IAsyncDisposable) cast which would
        // throw InvalidCastException on hosts that ship without IAsyncDisposable.
        // DbContext async disposal still happens via CreateAsyncScope below.
        using var host = hostFactory();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        // Tenant schema precheck — a CLI run against a tenant that was never
        // provisioned should fail fast with a clear message instead of exploding
        // inside the seeder's first DbContext resolution. Tests inject a probe;
        // production uses the default Postgres pg_namespace lookup.
        var tenantId = options.TenantId!.Value;
        var probe = tenantSchemaProbe ?? DefaultTenantSchemaProbeAsync;
        if (!await probe(services, tenantId, ct))
        {
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_tenant_schema_missing_template"),
                tenantId));
            console.WriteLine(CliLocalization.T("lockey_cli_demoload_tenant_schema_missing_pointer"));
            return CliDispatcher.UsageError;
        }

        if (options.DryRun)
        {
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_dryrun_header_template"),
                tenantId, options.Scenario));
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_dryrun_plan_template"),
                CountModules(services)));
            console.WriteLine(CliLocalization.T("lockey_cli_demoload_dryrun_no_changes"));
            return 0;
        }

        var seeder = services.GetRequiredService<IDemoDataSeeder>();
        var result = await seeder.SeedAsync(
            tenantId.ToString(), options.Scenario!, ct);

        console.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            CliLocalization.T("lockey_cli_demoload_completed_template"),
            tenantId, options.Scenario));
        foreach (var outcome in result.Modules)
        {
            var label = outcome.Status switch
            {
                DemoSeedStatus.Seeded => CliLocalization.T("lockey_cli_demoload_outcome_seeded"),
                DemoSeedStatus.AlreadySeeded => CliLocalization.T("lockey_cli_demoload_outcome_already_seeded"),
                DemoSeedStatus.NoOp => CliLocalization.T("lockey_cli_demoload_outcome_noop"),
                DemoSeedStatus.Failed => string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CliLocalization.T("lockey_cli_demoload_outcome_failed_template"),
                    outcome.ErrorMessage),
                _ => outcome.Status.ToString()
            };
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_outcome_line_template"),
                outcome.ModuleName, label));
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
        bool verbose = false;
        var unknown = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--dry-run") { dryRun = true; continue; }
            if (a == "--verbose" || a == "-v") { verbose = true; continue; }

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

        return new DemoLoadOptions(tenantId, scenario, dryRun, verbose, unknown);
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
        if (current == name && i + 1 < args.Length && !IsFlag(args[i + 1]))
        {
            value = args[i + 1];
            i++;
            return true;
        }

        value = "";
        return false;
    }

    /// <summary>
    /// Treat a token as a flag when it starts with <c>--</c> OR is a short
    /// form like <c>-v</c>. Does NOT treat bare <c>-</c> or negative numbers
    /// (<c>-1</c>, <c>-3.14</c>) as flags — those may be legitimate values.
    /// Keeps the parser honest: previously <c>--tenant -v</c> would silently
    /// eat <c>-v</c> as the tenant value.
    /// </summary>
    private static bool IsFlag(string token)
    {
        if (token.Length < 2 || token[0] != '-') return false;
        if (token.StartsWith("--", StringComparison.Ordinal)) return true;
        // Short flag: single dash + a non-digit, non-dot character (so -1, -3.14 stay values).
        return !char.IsDigit(token[1]) && token[1] != '.';
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
            await dbContext.Database.OpenConnectionAsync(ct);

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
                await dbContext.Database.CloseConnectionAsync();
            // NB: the tenant we set above (tenantId.ToString()) is the same
            // tenant the seeder operates on, so the AsyncLocal accessor
            // inheriting it is intentional here. There is no "clear" API
            // on ITenantContextAccessor; wrapping the probe in a nested DI
            // scope would not clear the AsyncLocal either (AsyncLocal flows
            // across scopes). When the CLI verb finishes the process exits,
            // so there is no longer-lived context to pollute.
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
    bool Verbose,
    IReadOnlyList<string> UnknownArgs)
{
    public bool IsValid(out string error)
    {
        if (UnknownArgs.Count > 0)
        {
            error = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_demoload_invalid_args_template"),
                string.Join(", ", UnknownArgs));
            return false;
        }
        if (TenantId is null)
        {
            error = CliLocalization.T("lockey_cli_demoload_missing_tenant");
            return false;
        }
        if (string.IsNullOrWhiteSpace(Scenario))
        {
            error = CliLocalization.T("lockey_cli_demoload_missing_scenario");
            return false;
        }
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
