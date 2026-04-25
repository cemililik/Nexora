using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Infrastructure.Modules;
using Nexora.SharedKernel.Abstractions.Migrations;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Npgsql;

namespace Nexora.Infrastructure.Migrations;

/// <summary>
/// Default <see cref="IMigrationRunner"/> implementation (T-011).
/// Wraps the per-module <see cref="IModuleMigration"/> mechanism with
/// the orchestration discipline from
/// <c>docs/operations/migration-orchestration.md</c> §2:
///
/// <list type="number">
///   <item>Validate the tenant id, derive the schema name (<c>tenant_{id}</c>).</item>
///   <item>Open a dedicated Npgsql session and call
///         <c>pg_try_advisory_lock</c> over a stable per-tenant key
///         (FNV-1a-64 of <c>"migrate:" + tenantId</c>, computed
///         client-side via <see cref="ComputeLockKey"/> — does NOT
///         match Postgres <c>hashtext</c>). The runner polls with a
///         5-second budget instead of blocking; if the lock is busy the
///         result carries <see cref="MigrationRunStatus.LockNotAcquired"/>
///         and the caller retries later. Release on dispose.</item>
///   <item>Iterate registered <see cref="IModule"/>s in dependency order
///         (re-using <see cref="DemoDataSeeder.OrderByDependencies"/> —
///         the platform's only topo-sort over the same graph). For each:
///         resolve the module's <see cref="IModuleMigration"/> if registered
///         and invoke <c>MigrateAsync(schema, ct)</c>.</item>
///   <item>On the first module failure, log a <see cref="MigrationFailure"/>
///         row, mark the tenant <c>MigrationFailed</c> via the Identity
///         <c>Tenants</c> table, mark every remaining module as
///         <see cref="MigrationModuleStatus.Skipped"/>, and return a
///         <see cref="MigrationRunStatus.Failed"/> result. Caller (e.g.
///         <c>platform:migrate-tenants</c>) reads the result; nothing
///         throws.</item>
/// </list>
///
/// <para>
/// Failure recording is platform-scoped (lives in the public schema via
/// <see cref="MigrationFailureLogDbContext"/>) so a row is writable even
/// when the tenant schema itself is unreachable. The
/// <c>identity_tenants</c> status update goes through the same Npgsql
/// session that holds the advisory lock so a downstream API request
/// reading the tenant state cannot race past the gate.
/// </para>
/// </summary>
public sealed class MigrationRunner(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IModule> modules,
    ILogger<MigrationRunner> logger,
    string platformConnectionString) : IMigrationRunner
{
    private const int AdvisoryLockTimeoutSeconds = 5;

    /// <inheritdoc />
    public async Task<MigrationRunResult> MigrateAllModulesAsync(
        string tenantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        if (!Guid.TryParse(tenantId, out var tenantGuid))
            throw new ArgumentException(
                "lockey_migration_runner_tenant_id_must_be_guid", nameof(tenantId));

        var schemaName = $"tenant_{tenantGuid}";
        var ordered = DemoDataSeeder.OrderByDependencies(modules.ToList());

        // Open a dedicated session for the advisory lock + downstream
        // tenant-status update. We don't reuse the EF Core connection pool
        // because pg_advisory_lock is session-scoped and a pooled
        // connection's lifetime is unpredictable.
        await using var lockConn = new NpgsqlConnection(platformConnectionString);
        await lockConn.OpenAsync(ct);

        var lockKey = ComputeLockKey(tenantGuid);
        if (!await TryAcquireAdvisoryLockAsync(lockConn, lockKey, ct))
        {
            logger.LogWarning(
                "Migration: could not acquire advisory lock for tenant {TenantId} within {Timeout}s — another runner is in flight; caller should retry later.",
                tenantGuid, AdvisoryLockTimeoutSeconds);
            return new MigrationRunResult(
                tenantId,
                MigrationRunStatus.LockNotAcquired,
                ordered.Select(m => new MigrationModuleOutcome(m.Name, MigrationModuleStatus.Skipped)).ToList());
        }

        // T-013: stamp the run-start timestamp on identity_tenants so the
        // nightly drift audit (PlatformAuditMigrationDriftJob) can suppress
        // alerts during the rolling-migration window. Direct UPDATE — same
        // pattern + same connection as MarkTenantMigrationFailedAsync —
        // because MigrationRunner must not depend on the Identity module
        // assembly. Failures here log + continue: the migration proceeds
        // and at worst the next drift audit fires an alert sooner.
        await StampMigrationStartedAsync(lockConn, tenantGuid, ct);

        try
        {
            return await RunModulesUnderLockAsync(tenantId, tenantGuid, schemaName, ordered, lockConn, ct);
        }
        finally
        {
            // Use CancellationToken.None: if ct is already cancelled, passing
            // it here would throw OperationCanceledException and mask the
            // Succeeded/Failed result that was already determined above.
            await ReleaseAdvisoryLockAsync(lockConn, lockKey, CancellationToken.None);
        }
    }

    private async Task StampMigrationStartedAsync(
        NpgsqlConnection conn, Guid tenantGuid, CancellationToken ct)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            // AND "IsDeleted" = false matches the soft-delete predicate used
            // throughout identity_tenants writes; AND "Status" <> 'Terminated'
            // skips terminated tenants (no future migrations expected).
            cmd.CommandText =
                """
                UPDATE public.identity_tenants
                SET "LastMigrationStartedAtUtc" = now() AT TIME ZONE 'utc'
                WHERE "Id" = @tenantId
                  AND "IsDeleted" = false
                  AND "Status" <> 'Terminated';
                """;
            cmd.Parameters.AddWithValue("tenantId", tenantGuid);
            var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
            if (affectedRows == 0)
            {
                // Tenant row missing or already Terminated / soft-deleted —
                // the WHERE clause filtered it out. Surface at Debug so
                // operators tailing the log see why drift suppression
                // won't engage for this tenant on the upcoming sweep,
                // without escalating an effectively-benign no-op.
                logger.LogDebug(
                    "MigrationRunner: LastMigrationStartedAtUtc UPDATE matched 0 rows for tenant {TenantId} (terminated, soft-deleted, or never provisioned).",
                    tenantGuid);
            }
        }
        catch (NpgsqlException ex)
        {
            logger.LogWarning(ex,
                "MigrationRunner: failed to stamp LastMigrationStartedAtUtc for tenant {TenantId} — drift audit may alert sooner during this rolling-migration window, but the migration itself proceeds.",
                tenantGuid);
        }
        catch (System.Data.Common.DbException ex)
        {
            logger.LogWarning(ex,
                "MigrationRunner: failed to stamp LastMigrationStartedAtUtc for tenant {TenantId} — drift audit may alert sooner during this rolling-migration window, but the migration itself proceeds.",
                tenantGuid);
        }
    }

    private async Task<MigrationRunResult> RunModulesUnderLockAsync(
        string tenantId,
        Guid tenantGuid,
        string schemaName,
        IList<IModule> ordered,
        NpgsqlConnection lockConn,
        CancellationToken ct)
    {
        var outcomes = new List<MigrationModuleOutcome>(ordered.Count);
        IModule? failedModule = null;
        Exception? failure = null;

        foreach (var module in ordered)
        {
            ct.ThrowIfCancellationRequested();

            // Once any module has failed, every subsequent module is
            // marked Skipped without invocation. Continuing past a
            // failure would leave the tenant in a half-migrated state
            // that is harder to recover from than "the tenant is
            // quarantined; ops fixes the failing migration; we retry."
            if (failedModule is not null)
            {
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Skipped));
                continue;
            }

            try
            {
                await ApplyModuleMigrationAsync(module, schemaName, ct);
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Migrated));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            // Narrow exception families: DbException (NpgsqlException derives
            // from it, so one branch covers both) for the SQL surface;
            // InvalidOperationException for EF model + DI misuse.
            // Anything outside these (StackOverflowException etc.) propagates.
            catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException)
            {
                failedModule = module;
                failure = ex;
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Failed, ex.Message));
            }
        }

        if (failedModule is not null)
        {
            await PersistFailureAsync(tenantGuid, failedModule.Name, failure!, ct);
            await MarkTenantMigrationFailedAsync(lockConn, tenantGuid, ct);
            logger.LogError(failure!,
                "Migration FAILED for tenant {TenantId} on module {Module}. Tenant transitioned to MigrationFailed.",
                tenantGuid, failedModule.Name);
            return new MigrationRunResult(
                tenantId,
                MigrationRunStatus.Failed,
                outcomes,
                FailureModuleName: failedModule.Name,
                FailureMessage: failure!.Message);
        }

        logger.LogInformation(
            "Migration succeeded for tenant {TenantId} across {Count} modules.",
            tenantGuid, outcomes.Count);
        return new MigrationRunResult(tenantId, MigrationRunStatus.Succeeded, outcomes);
    }

    private async Task ApplyModuleMigrationAsync(IModule module, string schemaName, CancellationToken ct)
    {
        // Resolve the module's IModuleMigration if it ships one. Modules
        // without migrations (everything in Phase 1.5 — tables come from
        // DevelopmentSeed.ApplySchemaUpdatesAsync, not EF migrations yet)
        // are a no-op rather than a hard failure: the runner shipped in
        // T-011 is foundation; per-module IModuleMigration implementations
        // arrive with each Phase 2 module's first EF migration.
        await using var scope = scopeFactory.CreateAsyncScope();
        var migration = scope.ServiceProvider.GetServices<IModuleMigration>()
            .FirstOrDefault(m => string.Equals(m.ModuleName, module.Name, StringComparison.OrdinalIgnoreCase));
        if (migration is null)
        {
            logger.LogDebug(
                "Migration: module {Module} has no IModuleMigration registered — skipping (no-op).",
                module.Name);
            return;
        }
        await migration.MigrateAsync(schemaName, ct);
    }

    private async Task PersistFailureAsync(
        Guid tenantGuid, string moduleName, Exception exception, CancellationToken ct)
    {
        // Failure log lives in `public` (see MigrationFailureLogDbContext)
        // so the row is writable even when the tenant schema is broken.
        // Scope creation + GetRequiredService + SaveChanges are all inside the
        // try so any non-cancellation failure (DI misconfiguration, DB
        // unreachable, model error) is caught and logged without propagating
        // to the caller — this is a best-effort persistence; the
        // tenant-status update in MarkTenantMigrationFailedAsync is the
        // second line of defense.
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var failureDb = scope.ServiceProvider.GetRequiredService<MigrationFailureLogDbContext>();
            failureDb.Failures.Add(MigrationFailure.Create(tenantGuid, moduleName, exception));
            await failureDb.SaveChangesAsync(ct);
        }
        catch (Exception logEx) when (logEx is not OperationCanceledException)
        {
            logger.LogError(logEx,
                "Migration: persisting failure record for tenant {TenantId} module {Module} also failed — operator must inspect host logs to triage.",
                tenantGuid, moduleName);
        }
    }

    private async Task MarkTenantMigrationFailedAsync(
        NpgsqlConnection conn, Guid tenantGuid, CancellationToken ct)
    {
        // Direct UPDATE on `public.identity_tenants` rather than going
        // through the Identity Tenant aggregate: MigrationRunner is
        // platform infrastructure and must not depend on Identity's
        // module assembly (and therefore cannot raise the
        // TenantStatusChangedEvent that domain mutation would emit —
        // documented intent, not oversight). The Status column is
        // HasConversion<string> (see PlatformDbContext.OnModelCreating)
        // so the value must be the enum's string name, not its integer.
        //
        // <b>Failure-safe.</b> The caller invokes this AFTER the migration
        // failure has already been logged to platform_migration_failures;
        // a tenant-status UPDATE failure here would otherwise mask the
        // original failure with a secondary one. Catch + log the inner
        // failure and let the caller surface the migration's own
        // FailureMessage (review user finding + #69).
        try
        {
            await using var cmd = conn.CreateCommand();
            // AND "IsDeleted" = false guards soft-deleted tenants —
            // operator deleted the tenant after a migration was queued,
            // we must not resurrect its status row (review round-2).
            cmd.CommandText =
                """
                UPDATE public.identity_tenants
                SET "Status" = 'MigrationFailed',
                    "UpdatedAt" = now() AT TIME ZONE 'utc'
                WHERE "Id" = @tenantId
                  AND "Status" <> 'Terminated'
                  AND "IsDeleted" = false;
                """;
            cmd.Parameters.AddWithValue("tenantId", tenantGuid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex,
                "MigrationRunner: failed to mark tenant {TenantId} as MigrationFailed; " +
                "the original migration failure has already been recorded in " +
                "platform_migration_failures. Operator must transition the tenant manually.",
                tenantGuid);
        }
        catch (System.Data.Common.DbException ex)
        {
            logger.LogError(ex,
                "MigrationRunner: failed to mark tenant {TenantId} as MigrationFailed; " +
                "the original migration failure has already been recorded in " +
                "platform_migration_failures. Operator must transition the tenant manually.",
                tenantGuid);
        }
    }

    /// <summary>
    /// Computes the advisory-lock key for <paramref name="tenantGuid"/>.
    /// FNV-1a-64 of <c>"migrate:" + tenantGuid.ToString("D")</c>. Computed
    /// in C# rather than via SQL <c>hashtext</c> to avoid an extra
    /// round-trip on every Acquire — the values do NOT match PostgreSQL's
    /// own <c>hashtext</c> (Jenkins one-at-a-time, 32-bit), which earlier
    /// docs incorrectly claimed; that is fine because nothing else in the
    /// system reads this key (review #61, user finding). What matters
    /// is stability per (tenant, "migrate:" prefix) across processes +
    /// hosts, which FNV-1a-64 provides.
    /// </summary>
    internal static long ComputeLockKey(Guid tenantGuid)
    {
        // FNV-1a 64-bit over "migrate:" + guid string. Stable, fast,
        // distinct enough that two distinct tenant ids will not collide
        // in any realistic deployment (~1.8e19 key space). The advisory
        // lock API takes a `bigint`, so we pack the FNV-1a 64-bit result
        // verbatim.
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        var input = $"migrate:{tenantGuid:D}";
        ulong hash = fnvOffset;
        foreach (var ch in input)
        {
            hash ^= ch;
            unchecked { hash *= fnvPrime; }
        }
        return unchecked((long)hash);
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(
        NpgsqlConnection conn, long lockKey, CancellationToken ct)
    {
        // pg_try_advisory_lock returns boolean — true if acquired, false
        // if another session holds it. Wrap in a short retry loop so
        // transient contention does not immediately fail.
        for (int attempt = 0; attempt < AdvisoryLockTimeoutSeconds; attempt++)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
            cmd.Parameters.AddWithValue("key", lockKey);
            var result = await cmd.ExecuteScalarAsync(ct);
            if (result is true) return true;
            // No delay after the LAST attempt — falling through to
            // `return false` is the right path; an extra second buys
            // nothing and adds latency to the caller's failure response
            // (review #62).
            if (attempt < AdvisoryLockTimeoutSeconds - 1)
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        return false;
    }

    private static async Task ReleaseAdvisoryLockAsync(
        NpgsqlConnection conn, long lockKey, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
        cmd.Parameters.AddWithValue("key", lockKey);
        await cmd.ExecuteScalarAsync(ct);
    }
}
