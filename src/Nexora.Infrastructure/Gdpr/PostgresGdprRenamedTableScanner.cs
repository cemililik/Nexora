using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Gdpr;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Gdpr;

/// <summary>
/// Postgres-backed <see cref="IGdprRenamedTableScanner"/>. Discovers tables
/// in the current tenant's schema whose names match the canonical
/// <c>{module}_{entity}_del_{timestamp}</c> shape and dispatches the
/// caller's redaction callback. Implemented as a service-locator-free
/// helper that takes the caller's <see cref="DbContext"/> at the call site
/// — each module passes its own <see cref="DbContext"/> so the discovery
/// query and the redaction UPDATE share one connection (and one tenant
/// schema search_path).
/// </summary>
/// <remarks>
/// <para>
/// The discovery uses a Postgres regex match (<c>~</c>) instead of SQL
/// <c>LIKE</c> because <c>LIKE</c> treats <c>_</c> as a single-character
/// wildcard — <c>'contacts_%_del_%'</c> would match <c>contactz_*</c> and
/// modules with prefix-overlapping names would cross-bleed. The regex is
/// anchored at both ends and accepts both timestamp shapes T-026 may emit.
/// </para>
/// </remarks>
public sealed class PostgresGdprRenamedTableScanner<TDbContext>(
    TDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<PostgresGdprRenamedTableScanner<TDbContext>> logger)
    : IGdprRenamedTableScanner<TDbContext>
    where TDbContext : DbContext
{
    private static readonly Meter Meter = new("Nexora.Infrastructure.Gdpr", "1.0");

    /// <summary>Counter incremented per scan operation, tagged by module.</summary>
    public static readonly Counter<long> ScanCounter = Meter.CreateCounter<long>(
        "nexora_gdpr_erasure_renamed_table_scans_total", "scans",
        "Total renamed-table scan operations during GDPR erasure handling.");

    // Defence-in-depth: even though module names are validated at module
    // registration to be snake_case ASCII, escape any regex metacharacters
    // before interpolation.
    private static readonly Regex ModuleNameValidation = new(
        @"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    public async Task<GdprRenamedTableScanResult> ScanAsync(
        string moduleName,
        Func<string, CancellationToken, Task<int>> redactSingleTableAsync,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(redactSingleTableAsync);

        if (!ModuleNameValidation.IsMatch(moduleName))
            throw new ArgumentException(
                $"GDPR scanner: module name '{moduleName}' is not a valid snake-case slug.",
                nameof(moduleName));

        if (!dbContext.Database.IsRelational())
        {
            // EF InMemory in tests — the scanner is a no-op; the caller's
            // canonical-table redaction is the source of truth in those
            // suites. Counter still ticks so dashboards see the call.
            ScanCounter.Add(1, new KeyValuePair<string, object?>("module", moduleName));
            return GdprRenamedTableScanResult.Empty;
        }

        var schemaName = ResolveSchemaName();

        // Anchored regex — module-prefixed only, requires literal _del_,
        // accepts both timestamp shapes from T-025/T-026.
        var pattern = $"^{moduleName}_[a-z0-9_]+_del_([0-9]{{14}}|[0-9]{{8}}_[0-9]{{6}})$";

        var renamedTables = new List<string>();
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere) await connection.OpenAsync(ct);

        try
        {
            await using (var discoveryCmd = connection.CreateCommand())
            {
                discoveryCmd.CommandText = """
                    SELECT table_name FROM information_schema.tables
                    WHERE table_schema = @schemaName
                      AND table_name ~ @pattern
                    ORDER BY table_name
                    """;
                AddParameter(discoveryCmd, "@schemaName", schemaName);
                AddParameter(discoveryCmd, "@pattern", pattern);

                await using var reader = await discoveryCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    renamedTables.Add(reader.GetString(0));
            }
        }
        finally
        {
            if (openedHere) await connection.CloseAsync();
        }

        ScanCounter.Add(1, new KeyValuePair<string, object?>("module", moduleName));

        if (renamedTables.Count == 0)
            return GdprRenamedTableScanResult.Empty;

        var totalRedacted = 0;
        foreach (var table in renamedTables)
        {
            try
            {
                totalRedacted += await redactSingleTableAsync(table, ct);
            }
            catch (Npgsql.NpgsqlException ex)
            {
                // One renamed table failing must not abort the rest of the
                // scan — the canonical redaction has already happened, and
                // a stale renamed table that cannot be UPDATE-ed is better
                // surfaced as a logged warning than as a thrown exception
                // that fails the entire GDPR event handler.
                logger.LogWarning(ex,
                    "GDPR escape hatch: redaction failed for renamed table {Table} (module {Module}); other tables continue.",
                    table, moduleName);
            }
        }

        logger.LogInformation(
            "GDPR escape hatch: redacted {RowCount} rows across {RenamedTableCount} renamed tables for module {Module}",
            totalRedacted, renamedTables.Count, moduleName);

        return new GdprRenamedTableScanResult(renamedTables.Count, totalRedacted);
    }

    private string ResolveSchemaName()
    {
        var ctx = tenantContextAccessor.Current;
        if (!string.IsNullOrWhiteSpace(ctx.SchemaName)) return ctx.SchemaName;
        if (Guid.TryParse(ctx.TenantId, out var g)) return $"tenant_{g:N}";
        return ctx.TenantId;
    }

    private static void AddParameter(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
