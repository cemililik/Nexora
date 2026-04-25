using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Npgsql;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Infrastructure.Modules;

/// <summary>
/// Production <see cref="IUninstallAdvisoryLock"/> backed by
/// <c>pg_try_advisory_lock</c> on a dedicated session. The Npgsql connection
/// is owned by the returned handle and released on dispose so the lock
/// holds for the full cascade-uninstall sequence (per ADR-0031 / T-026).
/// </summary>
public sealed class PostgresUninstallAdvisoryLock(
    string connectionString,
    ILogger<PostgresUninstallAdvisoryLock> logger) : IUninstallAdvisoryLock
{
    // Same retry budget as MigrationRunner — five 1s polls. Cascade uninstall
    // is operator-driven; if another operator's cascade is mid-flight we'd
    // rather surface a "try again shortly" error than block the request.
    private const int AdvisoryLockTimeoutSeconds = 5;

    /// <summary>
    /// OpenTelemetry span for the advisory-lock acquisition path.
    /// External-call observability per OBSERVABILITY_STANDARDS — operators
    /// triaging "cascade lock starvation" can pull this from Tempo
    /// without adding ad-hoc logging (review #24).
    /// </summary>
    private static readonly ActivitySource ActivitySource =
        new("Nexora.Infrastructure.Modules.UninstallAdvisoryLock", "1.0");

    public async Task<IAsyncDisposable?> AcquireAsync(Guid tenantId, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity(
            "uninstall.advisory_lock.acquire", ActivityKind.Client);
        activity?.SetTag("nexora.tenant_id", tenantId);

        var lockKey = ComputeLockKey(tenantId);
        var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync(ct);
            for (var attempt = 0; attempt < AdvisoryLockTimeoutSeconds; attempt++)
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
                cmd.Parameters.AddWithValue("key", lockKey);
                var acquired = await cmd.ExecuteScalarAsync(ct);
                if (acquired is true)
                {
                    activity?.SetTag("nexora.lock.attempt", attempt + 1);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                    var handle = new Handle(conn, lockKey, tenantId, logger);
                    conn = null!; // ownership transferred — outer finally must NOT dispose.
                    return handle;
                }
                // Skip the delay after the LAST attempt — falling through
                // to the timeout warning + return null is the right path,
                // an extra second of waiting buys nothing (review #25 / #62).
                if (attempt < AdvisoryLockTimeoutSeconds - 1)
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            activity?.SetStatus(ActivityStatusCode.Error, "lock-busy");
            logger.LogWarning(
                "Cascade uninstall: could not acquire advisory lock for tenant {TenantId} within {Timeout}s.",
                tenantId, AdvisoryLockTimeoutSeconds);
            return null;
        }
        finally
        {
            if (conn is not null) await conn.DisposeAsync();
        }
    }

    /// <summary>
    /// FNV-1a-64 over <c>uninstall:{tenantId:D}</c>. Same shape as
    /// <c>MigrationRunner.ComputeLockKey</c> with a different prefix so a
    /// tenant under migration and a tenant under cascade-uninstall use
    /// different lock keys (different operations are independently lockable).
    /// </summary>
    internal static long ComputeLockKey(Guid tenantId)
    {
        const ulong fnvOffset = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;
        var input = $"uninstall:{tenantId:D}";
        var hash = fnvOffset;
        foreach (var ch in input)
        {
            hash ^= ch;
            unchecked { hash *= fnvPrime; }
        }
        return unchecked((long)hash);
    }

    private sealed class Handle(
        NpgsqlConnection conn, long lockKey, Guid tenantId, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
                cmd.Parameters.AddWithValue("key", lockKey);
                await cmd.ExecuteScalarAsync();
            }
            catch (NpgsqlException ex)
            {
                // Connection died — the lock is released automatically by
                // session termination, so log and continue rather than
                // letting dispose throw. Tenant id is structured so an
                // operator can grep for the failing cascade (review #26).
                logger.LogWarning(ex,
                    "Cascade uninstall: advisory unlock failed for tenant {TenantId}; relying on session-close release.",
                    tenantId);
            }
            finally
            {
                await conn.DisposeAsync();
            }
        }
    }
}
