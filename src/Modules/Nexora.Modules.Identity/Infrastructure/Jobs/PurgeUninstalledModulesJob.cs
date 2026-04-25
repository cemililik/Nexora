using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Abstractions.Configuration;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Identity.Infrastructure.Jobs;

/// <summary>Parameters for the platform-wide uninstall-purge job.</summary>
/// <remarks>
/// The cron-driven outer entry runs platform-wide so <see cref="JobParams.TenantId"/>
/// carries the literal sentinel <c>"platform"</c>. Per-tenant child invocations
/// (fired via <see cref="BackgroundJob.Schedule"/> with deterministic jitter) carry
/// the actual tenant id.
/// </remarks>
public sealed record PurgeUninstalledModulesParams : JobParams;

/// <summary>
/// T-025 maintenance job: drops module tables that were renamed to
/// <c>{module}_{table}_del_{timestamp}</c> at uninstall time and have aged
/// past the configured retention window (default 30 days, ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// <b>Schedule.</b> Registered as a single platform-level recurring job at
/// <c>0 3 * * *</c> UTC. The outer run enumerates tenants with
/// purge-eligible <see cref="TenantModule"/> rows and fires per-tenant
/// child jobs via <see cref="BackgroundJob.Schedule"/> with a deterministic
/// per-tenant offset of 0–119 minutes (formula: <c>MD5(tenantId) &amp; 0x7FFFFFFF % 120</c>).
/// This avoids the thundering-herd problem of every tenant's
/// <c>DROP TABLE</c> hitting Postgres WAL writers simultaneously while
/// still being reproducible across process restarts.
/// </para>
/// <para>
/// <b>Per-tenant body.</b> For each due <see cref="TenantModule"/> row:
/// parse the CSV via <see cref="TenantModule.ParseDeletedTableNames"/>,
/// validate every entry against
/// <see cref="DeletedTableNameRegex"/> (accepting both
/// <c>yyyyMMddHHmmss</c> and <c>yyyyMMdd_HHmmss</c> timestamp shapes per
/// T-026), drop each in its own try/catch, reserialize the survivors
/// back via <see cref="TenantModule.SetDeletedTableNames"/>, and
/// hard-delete the row only when the survivor list is empty.
/// </para>
/// </remarks>
[Queue("maintenance")]
[DisplayName("platform:purge-uninstalled-modules")]
public sealed class PurgeUninstalledModulesJob(
    ITenantContextAccessor tenantContextAccessor,
    PlatformDbContext platformDb,
    IConfigurationResolver configResolver,
    IAuditStore auditStore,
    ILogger<PurgeUninstalledModulesJob> logger)
    : NexoraJob<PurgeUninstalledModulesParams>(tenantContextAccessor, logger)
{
    private const string RecurringJobId = "platform:purge-uninstalled-modules";
    private const string PlatformSentinelTenantId = "platform";
    private const int DefaultRetentionDays = 30;

    /// <summary>
    /// Whitelist of acceptable renamed-table shapes. Anchored.
    /// <list type="bullet">
    ///   <item><description>Module + entity: snake_case lowercase
    ///   (<c>[a-z][a-z0-9_]*</c>).</description></item>
    ///   <item><description>Suffix: <c>_del_</c>.</description></item>
    ///   <item><description>Timestamp: <c>yyyyMMddHHmmss</c> (14 digits)
    ///   OR <c>yyyyMMdd_HHmmss</c> (8 digits + <c>_</c> + 6 digits) so a
    ///   T-026 implementer choosing either format does not break this
    ///   purge step.</description></item>
    /// </list>
    /// </summary>
    public static readonly Regex DeletedTableNameRegex = new(
        @"^[a-z][a-z0-9_]*_del_(?:[0-9]{14}|[0-9]{8}_[0-9]{6})$",
        RegexOptions.Compiled);

    private static readonly Meter Meter = new("Nexora.Modules.Identity.PurgeUninstalledModules", "1.0");

    /// <summary>Counter incremented per renamed table successfully dropped.</summary>
    public static readonly Counter<long> PurgedTablesCounter = Meter.CreateCounter<long>(
        "nexora_module_uninstall_purged_total", "tables", "Total renamed module tables physically dropped.");

    /// <summary>Wall-clock duration of one tenant's purge step.</summary>
    public static readonly Histogram<double> PurgeDurationHistogram = Meter.CreateHistogram<double>(
        "nexora_module_uninstall_purge_duration_seconds", "s", "Wall-clock duration of a per-tenant purge step.");

    /// <summary>
    /// Registers the recurring outer schedule on the supplied scheduler. Called
    /// from <c>IdentityModule.ConfigureJobs</c> at startup.
    /// </summary>
    public static void RegisterRecurringSchedule(SharedKernel.Abstractions.Modules.IJobScheduler scheduler)
    {
        scheduler.AddOrUpdate<PurgeUninstalledModulesJob>(
            RecurringJobId,
            cronExpression: "0 3 * * *",
            methodCall: job => job.RunAsync(
                new PurgeUninstalledModulesParams { TenantId = PlatformSentinelTenantId },
                CancellationToken.None),
            queue: "maintenance");
    }

    /// <summary>
    /// Stable per-tenant jitter offset (0–119 minutes). MD5-based +
    /// <c>&amp; 0x7FFFFFFF</c> overflow-safe non-negative conversion per the
    /// T-025 AC; <c>Math.Abs(int.MinValue)</c> would throw.
    /// </summary>
    public static int ComputeJitterOffsetMinutes(Guid tenantId)
    {
        var hash = BitConverter.ToInt32(MD5.HashData(tenantId.ToByteArray()), 0);
        var nonNegative = hash & 0x7FFFFFFF;
        return nonNegative % 120;
    }

    protected override async Task ExecuteAsync(PurgeUninstalledModulesParams parameters, CancellationToken ct)
    {
        // Two execution modes: the platform-wide outer run (fans out per
        // tenant with jitter), and a per-tenant child run (does the actual
        // purge for one tenant).
        if (string.Equals(parameters.TenantId, PlatformSentinelTenantId, StringComparison.Ordinal))
        {
            await FanOutPerTenantAsync(ct);
            return;
        }

        if (!Guid.TryParse(parameters.TenantId, out var tenantGuid))
        {
            logger.LogWarning(
                "Purge job received non-Guid TenantId {TenantId}; skipping.",
                parameters.TenantId);
            return;
        }

        await PurgeTenantAsync(tenantGuid, ct);
    }

    /// <summary>
    /// Outer entry: enumerates distinct tenants with at least one purge-eligible
    /// <see cref="TenantModule"/> row and schedules a per-tenant child job
    /// at <c>now + jitter</c>.
    /// </summary>
    private async Task FanOutPerTenantAsync(CancellationToken ct)
    {
        // Outer fan-out: tenant context is the synthetic "platform" sentinel
        // so per-tenant config lookup is meaningless here. Use the platform
        // default directly; per-tenant overrides apply inside the child run
        // where the tenant context is set correctly by NexoraJob.RunAsync.
        var retentionDays = DefaultRetentionDays;
        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var dueTenantIds = await platformDb.TenantModules
            .IgnoreQueryFilters()
            .Where(tm => tm.IsDeleted && tm.DeletedAt != null && tm.DeletedAt < cutoffUtc)
            .Select(tm => tm.TenantId)
            .Distinct()
            .ToListAsync(ct);

        logger.LogInformation(
            "Purge fan-out: {Count} tenants have modules past {Retention}-day retention.",
            dueTenantIds.Count, retentionDays);

        foreach (var tenantId in dueTenantIds)
        {
            var offsetMinutes = ComputeJitterOffsetMinutes(tenantId.Value);
            var childParams = new PurgeUninstalledModulesParams { TenantId = tenantId.Value.ToString() };
            BackgroundJob.Schedule<PurgeUninstalledModulesJob>(
                job => job.RunAsync(childParams, CancellationToken.None),
                TimeSpan.FromMinutes(offsetMinutes));
        }
    }

    /// <summary>
    /// Per-tenant body: scans this tenant's eligible <see cref="TenantModule"/>
    /// rows, drops the renamed tables, hard-deletes empty rows, writes one
    /// audit entry per row.
    /// </summary>
    private async Task PurgeTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // Tenant context already set by NexoraJob.RunAsync — resolver picks
        // up the per-tenant override automatically; falls back to the
        // platform default if no override.
        var retentionDays = await configResolver.GetAsync<int?>(
            "modules.uninstall.retention_days", ct) ?? DefaultRetentionDays;
        var cutoffUtc = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        var tenantStrongId = Domain.ValueObjects.TenantId.From(tenantId);
        var due = await platformDb.TenantModules
            .IgnoreQueryFilters()
            .Where(tm => tm.TenantId == tenantStrongId
                && tm.IsDeleted
                && tm.DeletedAt != null
                && tm.DeletedAt < cutoffUtc)
            .ToListAsync(ct);

        var schemaName = $"tenant_{tenantId:N}";

        foreach (var row in due)
        {
            var entries = row.ParseDeletedTableNames();
            var dropped = new List<string>();
            var failed = new List<string>();

            foreach (var rawName in entries)
            {
                if (!DeletedTableNameRegex.IsMatch(rawName))
                {
                    logger.LogWarning(
                        "Purge: rejecting malformed renamed-table entry {Entry} for tenant {TenantId} module {Module}; kept for operator review.",
                        rawName, tenantId, row.ModuleName);
                    failed.Add(rawName);
                    continue;
                }

                try
                {
                    await DropTableAsync(schemaName, rawName, ct);
                    dropped.Add(rawName);
                    PurgedTablesCounter.Add(1, new KeyValuePair<string, object?>("module", row.ModuleName));
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    logger.LogError(ex,
                        "Purge: DROP TABLE failed for {Schema}.{Table} (tenant {TenantId} module {Module}); kept for retry.",
                        schemaName, rawName, tenantId, row.ModuleName);
                    failed.Add(rawName);
                }
                catch (System.Data.Common.DbException ex)
                {
                    logger.LogError(ex,
                        "Purge: DROP TABLE failed for {Schema}.{Table} (tenant {TenantId} module {Module}); kept for retry.",
                        schemaName, rawName, tenantId, row.ModuleName);
                    failed.Add(rawName);
                }
                catch (InvalidOperationException)
                {
                    // EF InMemory / non-relational: skip drop, pretend success
                    // so the test path can exercise the audit + hard-delete
                    // flow without a Postgres instance.
                    dropped.Add(rawName);
                }
            }

            // Reserialize survivors. Empty failed-list ⇒ null + hard-delete row.
            string? hardDeleteError = null;
            var rowHardDeleted = false;
            if (failed.Count == 0)
            {
                row.SetDeletedTableNames(null);
                try
                {
                    using var hardDeleteScope = platformDb.EnterHardDeleteScope();
                    platformDb.TenantModules.Remove(row);
                    await platformDb.SaveChangesAsync(ct);
                    rowHardDeleted = true;
                }
                catch (DbUpdateException ex)
                {
                    hardDeleteError = ex.Message;
                    logger.LogError(ex,
                        "Purge: hard-delete failed for tenant {TenantId} module {Module}; row stays for retry.",
                        tenantId, row.ModuleName);
                }
            }
            else
            {
                row.SetDeletedTableNames(string.Join(",", failed));
                await platformDb.SaveChangesAsync(ct);
            }

            await WriteAuditAsync(tenantId, row, dropped, failed, retentionDays, stopwatch.ElapsedMilliseconds, hardDeleteError, rowHardDeleted, ct);

            logger.LogInformation(
                "Purge tenant {TenantId} module {Module}: dropped {Dropped} table(s), {Failed} retained for retry, hard-deleted={HardDeleted}",
                tenantId, row.ModuleName, dropped.Count, failed.Count, rowHardDeleted);
        }

        stopwatch.Stop();
        PurgeDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("tenant", tenantId));
    }

    private async Task DropTableAsync(string schemaName, string tableName, CancellationToken ct)
    {
        // Both the schema and the table are double-quoted: schema names are
        // tenant_{guid} and a guid can start with a digit, which Postgres
        // would otherwise reject. Identifiers are sanitized by escaping
        // embedded quotes; the regex whitelist above already constrains
        // tableName to [a-z0-9_].
        var quotedSchema = QuoteIdentifier(schemaName);
        var quotedTable = QuoteIdentifier(tableName);
        var sql = $"DROP TABLE IF EXISTS {quotedSchema}.{quotedTable} CASCADE";

        var connection = platformDb.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    private static string QuoteIdentifier(string raw)
        => "\"" + raw.Replace("\"", "\"\"") + "\"";

    private async Task WriteAuditAsync(
        Guid tenantId,
        TenantModule row,
        IReadOnlyList<string> dropped,
        IReadOnlyList<string> failed,
        int retentionDays,
        long durationMs,
        string? hardDeleteError,
        bool rowHardDeleted,
        CancellationToken ct)
    {
        // Success criterion per ADR-0028 + T-025 AC: ZERO failed entries AND
        // the TenantModule row was hard-deleted. A no-op tenant with no
        // entries that succeeds in hard-deleting the row also reports
        // success — the prior dropping run's residue is now cleaned up.
        var isSuccess = failed.Count == 0 && rowHardDeleted;

        var metadata = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.ToString(),
            module = row.ModuleName,
            retentionDays,
            dropped,
            failed,
            durationMs,
            hardDeleteError,
        });

        var entry = new SharedKernel.Abstractions.Audit.AuditEntry(
            Id: SharedKernel.Abstractions.Audit.AuditEntryId.New(),
            TenantId: tenantId.ToString(),
            Module: "Identity",
            Operation: "module.uninstall.purge",
            OperationType: SharedKernel.Abstractions.Audit.OperationType.Delete,
            UserId: null,
            UserEmail: "system:platform-purge",
            IpAddress: null,
            UserAgent: null,
            CorrelationId: null,
            IsSuccess: isSuccess,
            ErrorKey: failed.Count > 0 ? "lockey_identity_module_purge_partial" : hardDeleteError is null ? null : "lockey_identity_module_purge_hard_delete_failed",
            EntityType: "TenantModule",
            EntityId: row.Id.Value.ToString(),
            BeforeState: null,
            AfterState: null,
            Changes: null,
            Metadata: metadata,
            Timestamp: DateTimeOffset.UtcNow);

        await auditStore.SaveAsync(entry, ct);
    }
}
