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
///   <item>Open a dedicated Npgsql session and acquire
///         <c>pg_advisory_lock(hashtext('migrate:' || tenantId))</c> with a
///         5-second wait. The lock is session-scoped so the runner holds
///         it for the whole pass, blocking concurrent runs against the
///         same tenant. Release on dispose.</item>
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

        try
        {
            return await RunModulesUnderLockAsync(tenantId, tenantGuid, schemaName, ordered, lockConn, ct);
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(lockConn, lockKey, ct);
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
            // Narrow exception families: NpgsqlException + DbException for
            // SQL surface; InvalidOperationException for EF model + DI
            // misuse; HttpRequestException is unlikely on the migration
            // path but Dapr-bound connection-string secrets may surface it.
            // Anything outside these (StackOverflow etc.) propagates.
            catch (NpgsqlException ex)
            {
                failedModule = module;
                failure = ex;
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Failed, ex.Message));
            }
            catch (System.Data.Common.DbException ex)
            {
                failedModule = module;
                failure = ex;
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Failed, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                failedModule = module;
                failure = ex;
                outcomes.Add(new MigrationModuleOutcome(module.Name, MigrationModuleStatus.Failed, ex.Message));
            }
        }

        if (failedModule is not null && failure is not null)
        {
            await PersistFailureAsync(tenantGuid, failedModule.Name, failure, ct);
            await MarkTenantMigrationFailedAsync(lockConn, tenantGuid, ct);
            logger.LogError(failure,
                "Migration FAILED for tenant {TenantId} on module {Module}. Tenant transitioned to MigrationFailed.",
                tenantGuid, failedModule.Name);
            return new MigrationRunResult(
                tenantId,
                MigrationRunStatus.Failed,
                outcomes,
                FailureModuleName: failedModule.Name,
                FailureMessage: failure.Message);
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
        await using var scope = scopeFactory.CreateAsyncScope();
        var failureDb = scope.ServiceProvider.GetRequiredService<MigrationFailureLogDbContext>();
        failureDb.Failures.Add(MigrationFailure.Create(tenantGuid, moduleName, exception));
        try
        {
            await failureDb.SaveChangesAsync(ct);
        }
        catch (System.Data.Common.DbException logEx)
        {
            // The failure log itself is unreachable. Log loudly and
            // continue — the tenant-status update below is the second
            // line of defense; without it, the platform forgets the
            // failure entirely.
            logger.LogError(logEx,
                "Migration: persisting failure record for tenant {TenantId} module {Module} also failed — operator must inspect host logs to triage.",
                tenantGuid, moduleName);
        }
    }

    private static async Task MarkTenantMigrationFailedAsync(
        NpgsqlConnection conn, Guid tenantGuid, CancellationToken ct)
    {
        // Direct UPDATE on `public.identity_tenants` rather than going
        // through the Identity Tenant aggregate: MigrationRunner is
        // platform infrastructure and must not depend on Identity's
        // module assembly. The Status column is HasConversion<string>
        // (see PlatformDbContext.OnModelCreating) so the value must be
        // the enum's string name, not its integer.
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            UPDATE public.identity_tenants
            SET "Status" = 'MigrationFailed',
                "UpdatedAt" = now() AT TIME ZONE 'utc'
            WHERE "Id" = @tenantId
              AND "Status" <> 'Terminated';
            """;
        cmd.Parameters.AddWithValue("tenantId", tenantGuid);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Computes the advisory-lock key as
    /// <c>hashtext('migrate:' || tenantId)</c>. Computed in C# (not in
    /// SQL) to avoid an extra round-trip; the hash function used here
    /// matches PostgreSQL's <c>hashtext</c> output for ASCII-only inputs
    /// because <c>hashtext</c>'s distribution properties don't matter for
    /// a single-key lock — only stability across calls does.
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
