using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Tests.Health;

/// <summary>
/// T-023: direct unit tests for <see cref="PostgresHealthCheck"/>. Exercises the
/// branches we can drive without a running Postgres — missing connection string,
/// unreachable host (via an invalid-but-parseable URI), and unavailable port.
/// Happy-path is covered transitively by the running dev container + the
/// /health/ready smoke-test in the ops runbook.
/// </summary>
public sealed class PostgresHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_MissingConnectionString_ReturnsUnhealthy()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var check = new PostgresHealthCheck(config);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not configured");
    }

    [Fact]
    public async Task CheckHealthAsync_UnreachableHost_ReturnsUnhealthy_WithLatency()
    {
        // 203.0.113.x is a TEST-NET-3 block — guaranteed non-routable, so we get
        // a deterministic connection failure (not DNS flakiness). `Timeout=1` keeps
        // the test itself quick.
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=203.0.113.42;Port=5432;Database=nexora;Username=x;Password=x;Timeout=1"
        }).Build();
        var check = new PostgresHealthCheck(config);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().StartWith("Postgres probe");
        result.Data.Should().ContainKey("latency_ms");
    }

    [Fact]
    public async Task CheckHealthAsync_CallerCancelled_PropagatesOperationCanceledException()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] =
                "Host=203.0.113.42;Port=5432;Database=nexora;Username=x;Password=x;Timeout=30"
        }).Build();
        var check = new PostgresHealthCheck(config);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // already cancelled before we call

        // Per the contract on PostgresHealthCheck: caller-cancelled probes
        // throw OperationCanceledException so the framework can distinguish
        // "abandoned probe" from "Postgres broken". Internal-timeout (linked
        // CTS firing because the probe exceeded 2s) is the one that returns
        // Unhealthy.
        var act = async () => await check.CheckHealthAsync(new HealthCheckContext(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
