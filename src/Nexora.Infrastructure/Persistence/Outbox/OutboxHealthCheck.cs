using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Health check that monitors the outbox message queue.
/// Reports Unhealthy when pending messages exceed 1000, Degraded when above 100 or any failed messages exist.
/// </summary>
public sealed class OutboxHealthCheck(OutboxDbContext dbContext) : IHealthCheck
{
    private const int UnhealthyThreshold = 1000;
    private const int DegradedThreshold = 100;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        var pendingCount = await dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedAt == null, ct);

        var failedCount = await dbContext.OutboxMessages
            .CountAsync(m => m.ProcessedAt == null && m.RetryCount >= 10, ct);

        var data = new Dictionary<string, object>
        {
            ["pending_count"] = pendingCount,
            ["failed_count"] = failedCount
        };

        if (pendingCount >= UnhealthyThreshold)
            return HealthCheckResult.Unhealthy($"Outbox has {pendingCount} pending messages", data: data);

        if (pendingCount >= DegradedThreshold || failedCount > 0)
            return HealthCheckResult.Degraded($"Outbox: {pendingCount} pending, {failedCount} failed", data: data);

        return HealthCheckResult.Healthy($"Outbox: {pendingCount} pending", data: data);
    }
}
