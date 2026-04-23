using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexora.Infrastructure.Messaging;
using NSubstitute;

namespace Nexora.Infrastructure.Tests.Health;

/// <summary>
/// T-023: unit tests for <see cref="DaprSidecarHealthCheck"/> using a
/// <see cref="HttpClient"/> backed by a fake <see cref="HttpMessageHandler"/>
/// so we can exercise every branch without a real sidecar.
/// </summary>
public sealed class DaprSidecarHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_Returns200_Healthy()
    {
        var check = BuildCheck(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("latency_ms");
        result.Data.Should().ContainKey("status_code");
        ((int)result.Data["status_code"]).Should().Be(200);
    }

    [Fact]
    public async Task CheckHealthAsync_Returns503_Unhealthy_WithStatusCodeInData()
    {
        var check = BuildCheck(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("503");
        ((int)result.Data["status_code"]).Should().Be(503);
    }

    [Fact]
    public async Task CheckHealthAsync_Throws_Unhealthy_WithExceptionAttached()
    {
        var check = BuildCheck(new StubHandler(_ => throw new HttpRequestException("connection refused")));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("connection refused");
        result.Exception.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task CheckHealthAsync_UsesConfiguredHostAndPort()
    {
        string? capturedUrl = null;
        var handler = new StubHandler(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var check = BuildCheck(handler, host: "sidecar.internal", httpPort: "3501");
        await check.CheckHealthAsync(new HealthCheckContext());

        capturedUrl.Should().Be("http://sidecar.internal:3501/v1.0/healthz");
    }

    // --- Helpers ---------------------------------------------------------------

    private static DaprSidecarHealthCheck BuildCheck(
        HttpMessageHandler handler, string host = "localhost", string httpPort = "3500")
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(DaprSidecarHealthCheck.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dapr:Host"] = host,
            ["Dapr:HttpPort"] = httpPort
        }).Build();

        return new DaprSidecarHealthCheck(factory, config);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
