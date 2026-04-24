using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// T-023: readiness health check that proves the API can reach its primary
/// Postgres instance. Runs <c>SELECT 1</c> against the <c>Default</c> connection
/// string with a 2-second timeout. Uses <see cref="NpgsqlConnection"/> directly
/// (not EF Core) so a mis-wired DbContext cannot mask a real connectivity issue.
///
/// <para>
/// <b>Cancellation contract:</b> caller-cancelled probes (when the supplied
/// <paramref name="ct"/> is cancelled) propagate <see cref="OperationCanceledException"/>
/// — the framework treats those distinctly from "infrastructure unhealthy".
/// Internal timeout (probe exceeded the 2-second budget) is reported as
/// Unhealthy with a clear description.
/// </para>
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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(Timeout);

        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(timeoutCts.Token);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            // Defensive int conversion: a sub-second TimeSpan would truncate
            // to 0 — which Npgsql treats as "infinite timeout", the opposite
            // of what we want. Math.Max(1, …) + Math.Ceiling guarantees we
            // never accidentally disable the timeout. The linked CTS above
            // is the primary timeout; this is belt-and-braces.
            cmd.CommandTimeout = Math.Max(1, (int)Math.Ceiling(Timeout.TotalSeconds));
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
        // Caller cancelled — propagate so the framework treats it as
        // "probe was abandoned", not "Postgres is broken".
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            throw;
        }
        // Internal timeout (linked CTS fired) — distinct unhealthy reason.
        catch (OperationCanceledException)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Postgres probe timed out after {Timeout.TotalSeconds:F0}s",
                data: new Dictionary<string, object> { ["latency_ms"] = sw.Elapsed.TotalMilliseconds });
        }
        catch (NpgsqlException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (TimeoutException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (ArgumentException ex)
        {
            // Malformed connection string surfaces as ArgumentException at
            // NpgsqlConnection construction.
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (FormatException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }

        HealthCheckResult Unhealthy(Exception ex)
            // Description deliberately stays generic — same rationale as
            // DaprSidecarHealthCheck: never echo a server-supplied message
            // into the readiness envelope (can leak SQL fragments, server
            // hostnames, internal infra info). The exception is attached
            // for in-process logging via the framework; data carries the
            // type name + latency for dashboards.
            => HealthCheckResult.Unhealthy(
                "Postgres probe failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                    ["error_type"] = ex.GetType().Name
                });
    }
}
