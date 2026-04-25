using System.Data.Common;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Host.Cli;

/// <summary>
/// <c>nexora demo:clean</c> command (T-009). Removes demo-seeded rows for a
/// tenant (module-by-module cleanup via <see cref="IDemoDataCleaner"/>) OR,
/// with <c>--drop-tenant --yes</c>, drops the entire tenant schema. Mirrors
/// <see cref="DemoLoadCommand"/>'s dispatch + parse shape so operators have
/// one mental model across verbs.
/// </summary>
public static class DemoCleanCommand
{
    /// <summary>
    /// Entry point called by <see cref="CliDispatcher"/> — pass all args AFTER the
    /// <c>demo:clean</c> verb.
    /// </summary>
    public static int Run(ReadOnlySpan<string> args)
    {
        var argv = args.ToArray();
        var parsed = ParseArgs(argv);
        try
        {
            return RunAsync(parsed, () => DemoLoadHostFactory.Build(argv))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        // Same narrow catch family as demo:load — CLI is the outermost process
        // boundary; unexpected exceptions propagate and crash the process loudly.
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(CliLocalization.T("lockey_cli_democlean_cancelled"));
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

    private static void WriteFailure(Exception ex, bool verbose)
    {
        if (verbose)
        {
            Console.Error.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_democlean_failed_verbose_template"),
                ex.Message, ex.GetType().Name));
            return;
        }
        Console.Error.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            CliLocalization.T("lockey_cli_democlean_failed_generic"),
            ex.GetType().Name));
    }

    internal static async Task<int> RunAsync(
        DemoCleanOptions options,
        Func<IHost> hostFactory,
        IConsole? console = null,
        CancellationToken ct = default)
    {
        console ??= SystemConsole.Instance;

        if (!options.IsValid(out var usageError))
        {
            console.WriteLine(usageError);
            console.WriteLine(CliLocalization.T("lockey_cli_democlean_usage_hint"));
            return CliDispatcher.UsageError;
        }

        using var host = hostFactory();
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var cleaner = services.GetRequiredService<IDemoDataCleaner>();
        var tenantId = options.TenantId!.Value;

        if (options.DropTenant)
        {
            // Destructive path — double-gated. The `--yes` flag is required
            // non-interactively; there is no TTY prompt. This keeps the CLI
            // scriptable while preventing a typo'd `--drop-tenant` from
            // nuking a tenant silently. Warning + outcome lines stream on
            // stderr so operators running `... | tee run.log` still see
            // the destruction notice even if stdout is redirected
            // elsewhere.
            if (options.DryRun)
            {
                // `--drop-tenant --dry-run` previews the drop without
                // calling cleaner.DropTenantAsync. The operator's stdin
                // commitment (via --yes) is honoured as a precondition
                // but no schema is touched. Mirrors the normal-cleanup
                // dry-run convention documented in cli.md §3.3 matrix.
                console.WriteErrorLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CliLocalization.T("lockey_cli_democlean_droptenant_dryrun_template"),
                    tenantId));
                return 0;
            }
            console.WriteErrorLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_democlean_droptenant_warning_template"),
                tenantId));
            var result = await cleaner.DropTenantAsync(tenantId.ToString(), ct);
            if (result.SchemaDropped && result.ErrorMessage is null)
            {
                console.WriteErrorLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CliLocalization.T("lockey_cli_democlean_droptenant_completed_template"),
                    tenantId));
                return 0;
            }
            if (result.SchemaDropped)
            {
                // Schema dropped but event publish failed — partial outcome
                // flagged to the operator so they can run external cleanup.
                console.WriteErrorLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CliLocalization.T("lockey_cli_democlean_droptenant_partial_template"),
                    tenantId, result.ErrorMessage));
                return CliDispatcher.PartialFailure;
            }
            console.WriteErrorLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_democlean_droptenant_failed_template"),
                tenantId,
                result.ErrorMessage ?? CliLocalization.T("lockey_cli_democlean_unknown_error")));
            return CliDispatcher.PartialFailure;
        }

        if (options.DryRun)
        {
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_democlean_dryrun_header_template"),
                tenantId, options.Scenario));
            console.WriteLine(CliLocalization.T("lockey_cli_democlean_dryrun_no_changes"));
            return 0;
        }

        var runResult = await cleaner.CleanAsync(tenantId.ToString(), options.Scenario!, ct);

        console.WriteLine(string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            CliLocalization.T("lockey_cli_democlean_completed_template"),
            tenantId, options.Scenario));
        foreach (var outcome in runResult.Modules)
        {
            var label = outcome.Status switch
            {
                DemoCleanStatus.Cleaned => CliLocalization.T("lockey_cli_democlean_outcome_cleaned"),
                DemoCleanStatus.NothingToClean => CliLocalization.T("lockey_cli_democlean_outcome_nothing"),
                DemoCleanStatus.NoOp => CliLocalization.T("lockey_cli_democlean_outcome_noop"),
                DemoCleanStatus.Failed => string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    CliLocalization.T("lockey_cli_democlean_outcome_failed_template"),
                    outcome.ErrorMessage),
                _ => outcome.Status.ToString()
            };
            console.WriteLine(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                CliLocalization.T("lockey_cli_democlean_outcome_line_template"),
                outcome.ModuleName, label));
        }

        return runResult.Modules.Any(m => m.Status == DemoCleanStatus.Failed)
            ? CliDispatcher.PartialFailure
            : 0;
    }

    internal static DemoCleanOptions ParseArgs(ReadOnlySpan<string> args)
    {
        Guid? tenantId = null;
        string? scenario = null;
        bool dropTenant = false;
        bool yes = false;
        bool dryRun = false;
        bool verbose = false;
        var unknown = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--dry-run") { dryRun = true; continue; }
            if (a == "--drop-tenant") { dropTenant = true; continue; }
            if (a == "--yes") { yes = true; continue; }
            if (a == "--verbose" || a == "-v") { verbose = true; continue; }

            if (TryConsumeFlag(args, ref i, a, "--tenant", out var tenantValue))
            {
                if (Guid.TryParse(tenantValue, out var guid))
                    tenantId = guid;
                else
                    unknown.Add(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        CliLocalization.T("lockey_cli_democlean_invalid_tenant_guid_template"),
                        tenantValue));
                continue;
            }

            if (TryConsumeFlag(args, ref i, a, "--scenario", out var scenarioValue))
            {
                scenario = scenarioValue;
                continue;
            }

            unknown.Add(a);
        }

        return new DemoCleanOptions(tenantId, scenario, dropTenant, yes, dryRun, verbose, unknown);
    }

    private static bool TryConsumeFlag(
        ReadOnlySpan<string> args, ref int i, string current, string name, out string value)
    {
        if (TryParseFlag(current, name, out value)) return true;
        if (current == name && i + 1 < args.Length && !IsFlag(args[i + 1]))
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

    private static bool IsFlag(string token)
    {
        if (token.Length < 2 || token[0] != '-') return false;
        if (token.StartsWith("--", StringComparison.Ordinal)) return true;
        return !char.IsDigit(token[1]) && token[1] != '.';
    }
}

/// <summary>Parsed options for <c>demo:clean</c>.</summary>
internal sealed record DemoCleanOptions(
    Guid? TenantId,
    string? Scenario,
    bool DropTenant,
    bool Yes,
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
                CliLocalization.T("lockey_cli_democlean_invalid_args_template"),
                string.Join(", ", UnknownArgs));
            return false;
        }
        if (TenantId is null)
        {
            error = CliLocalization.T("lockey_cli_democlean_missing_tenant");
            return false;
        }
        if (DropTenant)
        {
            if (!Yes)
            {
                error = CliLocalization.T("lockey_cli_democlean_droptenant_requires_yes");
                return false;
            }
            // --drop-tenant ignores --scenario (whole schema gone anyway).
            error = "";
            return true;
        }
        if (string.IsNullOrWhiteSpace(Scenario))
        {
            error = CliLocalization.T("lockey_cli_democlean_missing_scenario");
            return false;
        }
        error = "";
        return true;
    }
}
