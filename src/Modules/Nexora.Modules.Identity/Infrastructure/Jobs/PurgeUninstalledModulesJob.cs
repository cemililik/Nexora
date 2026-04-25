using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    IBackgroundJobClient backgroundJobClient,
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
    // Two mandatory segments required before _del_:
    //   [a-z][a-z0-9]* — module prefix (no internal underscores, e.g. "crm")
    //   _[a-z][a-z0-9_]* — table name (may contain underscores, e.g. "signature_recipients")
    // Single-segment names like "users_del_..." are rejected — they have no
    // module prefix and were never emitted by the canonical uninstall path.
    public static readonly Regex DeletedTableNameRegex = new(
        @"^[a-z][a-z0-9]*_[a-z][a-z0-9_]*_del_(?:[0-9]{14}|[0-9]{8}_[0-9]{6})$",
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
    /// Stable per-tenant jitter offset (0–119 minutes).
    /// FNV-1a-32 over the tenant GUID bytes — deterministic, non-crypto,
    /// never throws (unlike MD5 which can fail under FIPS policy), and
    /// produces a good distribution over the 120-minute window.
    /// <c>&amp; 0x7FFFFFFF</c> ensures a non-negative int before modulo.
    /// </summary>
    public static int ComputeJitterOffsetMinutes(Guid tenantId)
    {
        const uint fnvOffset = 2166136261u;
        const uint fnvPrime = 16777619u;
        var bytes = tenantId.ToByteArray();
        var hash = fnvOffset;
        foreach (var b in bytes)
        {
            hash ^= b;
            unchecked { hash *= fnvPrime; }
        }
        return (int)(hash & 0x7FFFFFFF) % 120;
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
        // Use a permissive 1-day cutoff for the fan-out so a tenant that
        // overrode retention to a value SHORTER than the platform default
        // (e.g. 7 days, 3 days, or 1 day) is still picked up. The child
        // run resolves the per-tenant override and re-applies it as the
        // authoritative cutoff before touching any rows. The earlier
        // hardcoded 30-day cutoff here silently dropped tenants whose
        // override was shorter than the platform default — they would
        // never make it into the fan-out (review #01 + user finding).
        var fanOutCutoffUtc = DateTimeOffset.UtcNow.AddDays(-MinFanOutRetentionDays);

        var dueTenantIds = await platformDb.TenantModules
            .IgnoreQueryFilters()
            .Where(tm => tm.IsDeleted && tm.DeletedAt != null && tm.DeletedAt < fanOutCutoffUtc)
            .Select(tm => tm.TenantId)
            .Distinct()
            .ToListAsync(ct);

        logger.LogInformation(
            "Purge fan-out: {Count} tenants candidate for purge (cutoff = {MinDays}-day; per-tenant override applied in child).",
            dueTenantIds.Count, MinFanOutRetentionDays);

        foreach (var tenantId in dueTenantIds)
        {
            var offsetMinutes = ComputeJitterOffsetMinutes(tenantId.Value);
            var childParams = new PurgeUninstalledModulesParams { TenantId = tenantId.Value.ToString() };
            backgroundJobClient.Schedule<PurgeUninstalledModulesJob>(
                job => job.RunAsync(childParams, CancellationToken.None),
                TimeSpan.FromMinutes(offsetMinutes));
        }
    }

    /// <summary>
    /// Hard floor for the fan-out cutoff. Anything shorter would let the
    /// outer enumerator pick up tenants whose retention has barely
    /// elapsed and add scheduling noise; anything longer and we'd miss
    /// tenants who overrode retention to less than the platform default.
    /// 1 day is the smallest meaningful retention surface and lines up
    /// with the per-tenant validation floor in PurgeTenantAsync.
    /// </summary>
    private const int MinFanOutRetentionDays = 1;
    private const int MinAllowedRetentionDays = 1;
    private const int MaxAllowedRetentionDays = 365;

    /// <summary>
    /// Per-tenant body: scans this tenant's eligible <see cref="TenantModule"/>
    /// rows, drops the renamed tables, hard-deletes empty rows, writes one
    /// audit entry per row.
    /// </summary>
    private async Task PurgeTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var (retentionDays, cutoffUtc) = await ResolveRetentionAsync(tenantId, ct);

        var tenantStrongId = Domain.ValueObjects.TenantId.From(tenantId);
        var due = await platformDb.TenantModules
            .IgnoreQueryFilters()
            .Where(tm => tm.TenantId == tenantStrongId
                && tm.IsDeleted
                && tm.DeletedAt != null
                && tm.DeletedAt < cutoffUtc)
            .ToListAsync(ct);

        // Canonical platform schema-name format is tenant_{guid:D} (with
        // hyphens) — set in CreateTenantCommand and TenantContext.
        // The earlier ":N" form here would target a non-existent schema and
        // every DROP TABLE would raise "schema does not exist", get swallowed
        // by the Npgsql catch, and the row would loop forever in the failed list.
        var schemaName = $"tenant_{tenantId}";

        // Branch on provider capability ONCE per tenant instead of once per
        // row — avoids repeated reflection inside the hot loop.
        var isRelational = platformDb.Database.IsRelational();

        foreach (var row in due)
            await PurgeModuleRowAsync(tenantId, schemaName, row, retentionDays, isRelational, ct);
    }

    /// <summary>
    /// Resolves and clamps the per-tenant retention-days config. Returned
    /// cutoff is computed at call time so all rows in a single tenant run
    /// share the same reference point.
    /// </summary>
    private async Task<(int RetentionDays, DateTimeOffset CutoffUtc)> ResolveRetentionAsync(
        Guid tenantId, CancellationToken ct)
    {
        // Tenant context already set by NexoraJob.RunAsync — resolver picks
        // up the per-tenant override automatically; falls back to the
        // platform default if no override.
        var rawRetention = await configResolver.GetAsync<int?>(
            "modules.uninstall.retention_days", ct) ?? DefaultRetentionDays;

        var retentionDays = rawRetention;
        if (retentionDays < MinAllowedRetentionDays || retentionDays > MaxAllowedRetentionDays)
        {
            // Operator misconfiguration (e.g. 0 or -1) would expand the
            // cutoff to "now or future" and immediately purge live windows.
            // Clamp + warn instead of letting a bad config silently delete
            // recent data.
            logger.LogWarning(
                "Purge: tenant {TenantId} retention override {Raw} is out of [{Min},{Max}] range; clamping to platform default {Default}.",
                tenantId, rawRetention, MinAllowedRetentionDays, MaxAllowedRetentionDays, DefaultRetentionDays);
            retentionDays = DefaultRetentionDays;
        }

        return (retentionDays, DateTimeOffset.UtcNow.AddDays(-retentionDays));
    }

    /// <summary>
    /// Processes a single <see cref="TenantModule"/> row: drops each renamed
    /// table, reserializes the survivor list (or hard-deletes the row when
    /// all tables dropped), then writes an audit entry.
    /// </summary>
    private async Task PurgeModuleRowAsync(
        Guid tenantId, string schemaName, TenantModule row,
        int retentionDays, bool isRelational, CancellationToken ct)
    {
        // Per-row stopwatch — the previous outer-scoped Stopwatch wrote
        // cumulative ElapsedMilliseconds into every audit entry, so the
        // last row in a multi-module purge looked far more expensive than
        // the first. Per-row makes the metadata accurate.
        var rowStopwatch = Stopwatch.StartNew();
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

            if (!isRelational)
            {
                // EF InMemory in tests: skip the SQL DROP and treat as
                // dropped so the audit + hard-delete path exercises
                // end-to-end. Production never lands here.
                dropped.Add(rawName);
                continue;
            }

            try
            {
                await DropTableAsync(schemaName, rawName, ct);
                dropped.Add(rawName);
                PurgedTablesCounter.Add(1, new KeyValuePair<string, object?>("module", row.ModuleName));
            }
            catch (System.Data.Common.DbException ex)
            {
                // NpgsqlException derives from DbException — one branch covers both.
                logger.LogError(ex,
                    "Purge: DROP TABLE failed for {Schema}.{Table} (tenant {TenantId} module {Module}); kept for retry.",
                    schemaName, rawName, tenantId, row.ModuleName);
                failed.Add(rawName);
            }
        }

        // Reserialize survivors. Empty failed-list ⇒ null + hard-delete row.
        // Both branches wrap SaveChanges in try/catch so a transient EF error
        // on ONE tenant's row cannot abort the rest of the outer foreach;
        // the audit row records the failure so operators see exactly which
        // step failed even when the row stays in place.
        string? error = null;
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
                error = ex.Message;
                logger.LogError(ex,
                    "Purge: hard-delete failed for tenant {TenantId} module {Module}; row stays for retry.",
                    tenantId, row.ModuleName);
            }
        }
        else
        {
            row.SetDeletedTableNames(string.Join(",", failed));
            try
            {
                await platformDb.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                error = ex.Message;
                logger.LogError(ex,
                    "Purge: SaveChanges of survivor list failed for tenant {TenantId} module {Module}; " +
                    "DROPs already succeeded so the audit will reflect them, but the row keeps the previous CSV until next purge.",
                    tenantId, row.ModuleName);
                // Detach so the half-saved entity doesn't poison the
                // next outer-loop iteration's SaveChanges.
                platformDb.Entry(row).State = EntityState.Detached;
            }
        }

        rowStopwatch.Stop();

        // Audit AFTER persistence so it records the final outcome. Wrapped
        // in its own catch so an audit-store outage does not abort the loop.
        var outcome = new PurgeRowOutcome(dropped, failed, retentionDays,
            rowStopwatch.ElapsedMilliseconds, error, rowHardDeleted);
        try
        {
            await WriteAuditAsync(tenantId, row, outcome, ct);
        }
        catch (Exception auditEx) when (auditEx is not OperationCanceledException)
        {
            // CLAUDE.md "no catch(Exception)" exempts background-job
            // boundaries — a single audit write failing must not abort
            // the rest of the outer foreach.
            logger.LogError(auditEx,
                "Purge: audit write failed for tenant {TenantId} module {Module}; continuing with next row.",
                tenantId, row.ModuleName);
        }

        PurgeDurationHistogram.Record(rowStopwatch.Elapsed.TotalSeconds,
            new KeyValuePair<string, object?>("tenant", tenantId),
            new KeyValuePair<string, object?>("module", row.ModuleName));

        logger.LogInformation(
            "Purge tenant {TenantId} module {Module}: dropped {Dropped} table(s), {Failed} retained for retry, hard-deleted={HardDeleted}",
            tenantId, row.ModuleName, dropped.Count, failed.Count, rowHardDeleted);
    }

    /// <summary>Outcome of one <see cref="TenantModule"/> purge step.</summary>
    private readonly record struct PurgeRowOutcome(
        IReadOnlyList<string> Dropped,
        IReadOnlyList<string> Failed,
        int RetentionDays,
        long DurationMs,
        string? Error,
        bool RowHardDeleted);

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
        Guid tenantId, TenantModule row, PurgeRowOutcome outcome, CancellationToken ct)
    {
        // Success criterion per ADR-0028 + T-025 AC: ZERO failed entries AND
        // the TenantModule row was hard-deleted. A no-op tenant with no
        // entries that succeeds in hard-deleting the row also reports
        // success — the prior dropping run's residue is now cleaned up.
        var isSuccess = outcome.Failed.Count == 0 && outcome.RowHardDeleted;

        var metadata = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.ToString(),
            module = row.ModuleName,
            retentionDays = outcome.RetentionDays,
            dropped = outcome.Dropped,
            failed = outcome.Failed,
            durationMs = outcome.DurationMs,
            hardDeleteError = outcome.Error,
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
            ErrorKey: outcome.Failed.Count > 0
                ? "lockey_identity_module_purge_partial"
                : outcome.Error is null ? null : "lockey_identity_module_purge_hard_delete_failed",
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
