using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nexora.Infrastructure.Messaging;

/// <summary>
/// T-023: readiness health check that proves the Dapr sidecar is reachable and
/// reports healthy. Hits <c>/v1.0/healthz</c> on the sidecar's HTTP port. A
/// Dapr outage is load-bearing for the API — state store, pub/sub, and secrets
/// all route through the sidecar, so the pod must fall out of rotation when
/// the sidecar is unhealthy.
///
/// <para>
/// The 1-second probe timeout is configured at DI registration time (see
/// <see cref="ProbeTimeout"/> + <c>InfrastructureServiceRegistration.AddNexoraInfrastructure</c>);
/// the check itself does not mutate the resolved <see cref="HttpClient"/>.
/// </para>
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

    /// <summary>
    /// Timeout applied to the readiness probe. Exposed so the DI registration
    /// can configure the named <see cref="HttpClient"/> once instead of the
    /// check mutating <see cref="HttpClient.Timeout"/> per call.
    /// </summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);

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
        // OperationCanceledException always rethrows — caller (Kubernetes
        // probe / hosted service) cancellation must NOT be turned into an
        // Unhealthy result; let the framework see the cancellation.
        catch (OperationCanceledException)
        {
            sw.Stop();
            throw;
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (IOException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return Unhealthy(ex);
        }

        HealthCheckResult Unhealthy(Exception ex)
        {
            // Description deliberately stays generic — "Dapr sidecar probe
            // failed" — so we never echo a server-supplied or implementation-
            // detail message back into the readiness envelope (which can leak
            // internal infra info to anything that polls /health/ready). The
            // exception itself is attached for in-process logging via the
            // health-check framework, where the operator-only audience is
            // appropriate.
            return HealthCheckResult.Unhealthy(
                "Dapr sidecar probe failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["latency_ms"] = sw.Elapsed.TotalMilliseconds,
                    ["url"] = url,
                    ["error_type"] = ex.GetType().Name
                });
        }
    }
}
