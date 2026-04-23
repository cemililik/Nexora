using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// T-023: readiness health check that proves the API can reach its primary
/// Postgres instance. Runs <c>SELECT 1</c> against the <c>Default</c> connection
/// string with a 2-second timeout. Uses <see cref="NpgsqlConnection"/> directly
/// (not EF Core) so a mis-wired DbContext cannot mask a real connectivity issue.
/// </summary>
public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy(
                "ConnectionStrings:Default is not configured",
                data: new Dictionary<string, object> { ["latency_ms"] = 0 });
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Timeout);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(timeoutCts.Token);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = (int)Timeout.TotalSeconds;
            var scalar = await cmd.ExecuteScalarAsync(timeoutCts.Token);

            sw.Stop();
            var data = new Dictionary<string, object>
            {
                ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                ["scalar"] = scalar ?? "<null>"
            };
            return HealthCheckResult.Healthy(
                $"Postgres reachable ({sw.Elapsed.TotalMilliseconds:F1} ms)",
                data: data);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Postgres probe timed out after {Timeout.TotalSeconds:F0}s",
                data: new Dictionary<string, object> { ["latency_ms"] = sw.Elapsed.TotalMilliseconds });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Postgres probe failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object> { ["latency_ms"] = sw.Elapsed.TotalMilliseconds });
        }
    }
}
