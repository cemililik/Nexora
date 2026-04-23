using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nexora.Infrastructure.Messaging;

/// <summary>
/// T-023: readiness health check that proves the Dapr sidecar is reachable and
/// reports healthy. Hits <c>/v1.0/healthz</c> on the sidecar's HTTP port with a
/// 1-second timeout. A Dapr outage is load-bearing for the API — state store,
/// pub/sub, and secrets all route through the sidecar, so the pod must fall out
/// of rotation when the sidecar is unhealthy.
/// </summary>
public sealed class DaprSidecarHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IHealthCheck
{
    /// <summary>
    /// Named <see cref="HttpClient"/> used by this check; declared here so
    /// registration code and the check share one source of truth.
    /// </summary>
    public const string HttpClientName = "dapr-healthz";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var host = configuration["Dapr:Host"] ?? "localhost";
        var port = configuration["Dapr:HttpPort"] ?? "3500";
        var url = $"http://{host}:{port}/v1.0/healthz";

        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            client.Timeout = Timeout;
            using var response = await client.GetAsync(url, ct);
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                ["status_code"] = (int)response.StatusCode,
                ["url"] = url
            };

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    $"Dapr sidecar reachable ({sw.Elapsed.TotalMilliseconds:F1} ms)",
                    data: data);
            }
            return HealthCheckResult.Unhealthy(
                $"Dapr sidecar returned HTTP {(int)response.StatusCode}",
                data: data);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Dapr sidecar probe timed out after {Timeout.TotalSeconds:F0}s",
                data: new Dictionary<string, object>
                {
                    ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                    ["url"] = url
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                $"Dapr sidecar probe failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                    ["url"] = url
                });
        }
    }
}
