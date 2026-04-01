using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Health check that monitors the outbox message queue.
/// Thresholds are configurable via <see cref="OutboxOptions.UnhealthyThreshold"/> and <see cref="OutboxOptions.DegradedThreshold"/>.
/// </summary>
public sealed class OutboxHealthCheck(
    OutboxDbContext dbContext,
    IOptions<OutboxOptions> options) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var opts = options.Value;

        var pendingCount = await dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedAt == null, ct);

        var failedCount = await dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedAt == null && m.RetryCount >= opts.MaxRetryCount, ct);

        var data = new Dictionary<string, object>
        {
            ["pending_count"] = pendingCount,
            ["failed_count"] = failedCount
        };

        if (pendingCount >= opts.UnhealthyThreshold)
            return HealthCheckResult.Unhealthy($"Outbox has {pendingCount} pending messages", data: data);

        if (pendingCount >= opts.DegradedThreshold || failedCount > 0)
            return HealthCheckResult.Degraded($"Outbox: {pendingCount} pending, {failedCount} failed", data: data);

        return HealthCheckResult.Healthy($"Outbox: {pendingCount} pending", data: data);
    }
}
