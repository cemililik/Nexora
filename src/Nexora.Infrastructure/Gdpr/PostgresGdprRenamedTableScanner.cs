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
    IMeterFactory meterFactory,
    ILogger<PostgresGdprRenamedTableScanner<TDbContext>> logger)
    : IGdprRenamedTableScanner<TDbContext>
    where TDbContext : DbContext
{
    // Instance counter created from the injected factory so each DI scope
    // gets its own Meter — enables proper test isolation and lifecycle
    // management (static Meter + Counter would outlive the scope).
    // Name follows OpenTelemetry conventions: dotted namespace, snake_case component.
    private readonly Counter<long> _scanCounter = meterFactory
        .Create("Nexora.Infrastructure.Gdpr", "1.0")
        .CreateCounter<long>(
            "nexora.gdpr.erasure.renamed_table_scans",
            "scans",
            "Total renamed-table scan operations during GDPR erasure handling.");

    // Defence-in-depth: even though module names are validated at module
    // registration to be snake_case ASCII, escape any regex metacharacters
    // before interpolation.
    private static readonly Regex ModuleNameValidation = new(
        @"^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<GdprRenamedTableScanResult> ScanAsync(
        string moduleName,
        Func<RenamedTableInfo, CancellationToken, Task<int>> redactSingleTableAsync,
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
            _scanCounter.Add(1, new KeyValuePair<string, object?>("module", moduleName));
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

        _scanCounter.Add(1, new KeyValuePair<string, object?>("module", moduleName));

        if (renamedTables.Count == 0)
            return GdprRenamedTableScanResult.Empty;

        // Build the discovered-set ONCE so the callback can pass-through
        // the same reference to every invocation without re-allocating
        // (handlers use this for sibling-table existence checks).
        var discoveredSet = (IReadOnlySet<string>)new HashSet<string>(renamedTables, StringComparer.Ordinal);
        var qualifiedSchema = QuoteIdentifier(schemaName);

        var totalRedacted = 0;
        foreach (var bareName in renamedTables)
        {
            var info = new RenamedTableInfo(
                BareName: bareName,
                QualifiedIdentifier: $"{qualifiedSchema}.{QuoteIdentifier(bareName)}",
                AllDiscoveredBareNames: discoveredSet);
            try
            {
                totalRedacted += await redactSingleTableAsync(info, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Catch any non-cancellation exception so one table's
                // failure does not abort the rest of the scan. Npgsql
                // errors (stale table, missing column) are the common
                // case, but domain / timeout / invalid-op exceptions
                // must also continue rather than propagating as a 500.
                // OperationCanceledException always re-propagates so the
                // handler honours the caller's cancellation token.
                logger.LogWarning(ex,
                    "GDPR escape hatch: redaction failed for renamed table {Table} (module {Module}); other tables continue.",
                    bareName, moduleName);
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
        // Canonical platform schema-name format is tenant_{guid:D} (with
        // hyphens) — set in CreateTenantCommand and TenantContext. The
        // earlier ":N" form here would diverge from the actual schema name
        // when accessor.SchemaName was empty.
        if (Guid.TryParse(ctx.TenantId, out var g)) return $"tenant_{g}";
        // Non-GUID TenantId: refuse to guess a schema name — caller has
        // ITenantContextAccessor wired but populated it with a raw string
        // that is neither a GUID nor a usable schema. Fail loud instead of
        // returning a value that would cause the next SQL discovery to hit
        // a schema we never own.
        throw new InvalidOperationException(
            $"GDPR scanner: tenant context has no SchemaName and TenantId '{ctx.TenantId}' is not a GUID; " +
            "cannot derive a tenant schema name. Caller must set ITenantContextAccessor with a populated SchemaName " +
            "or with a GUID-shaped TenantId.");
    }

    private static void AddParameter(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    /// <summary>
    /// PostgreSQL identifier quoting — wraps in double-quotes and escapes
    /// embedded quotes by doubling them. Used to build the
    /// schema-qualified <see cref="RenamedTableInfo.QualifiedIdentifier"/>
    /// so handler-side UPDATEs do not depend on the connection's
    /// <c>search_path</c> (review round-2 — search_path could be the
    /// tenant schema, the public schema, or neither depending on caller
    /// setup).
    /// </summary>
    private static string QuoteIdentifier(string raw)
        => "\"" + raw.Replace("\"", "\"\"") + "\"";
}
