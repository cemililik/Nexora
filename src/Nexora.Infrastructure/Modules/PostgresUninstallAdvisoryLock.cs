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

    public async Task<IAsyncDisposable?> AcquireAsync(Guid tenantId, CancellationToken ct)
    {
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
                    var handle = new Handle(conn, lockKey, logger);
                    conn = null!; // ownership transferred — outer finally must NOT dispose.
                    return handle;
                }
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

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
        NpgsqlConnection conn, long lockKey, ILogger logger) : IAsyncDisposable
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
                // letting dispose throw.
                logger.LogWarning(ex,
                    "Cascade uninstall: advisory unlock failed; relying on session-close release.");
            }
            finally
            {
                await conn.DisposeAsync();
            }
        }
    }
}
