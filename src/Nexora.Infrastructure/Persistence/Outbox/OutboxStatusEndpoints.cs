using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Admin-only endpoints for monitoring and managing the outbox queue.
/// Provides status overview and manual retry of failed messages.
/// </summary>
public static class OutboxStatusEndpoints
{
    /// <summary>Maps outbox status and management endpoints under /api/v1/internal/outbox.</summary>
    public static void MapOutboxStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/internal/outbox")
            .RequireAuthorization("identity.modules.manage"); // admin only

        group.MapGet("/status", async (OutboxDbContext dbContext, CancellationToken ct) =>
        {
            var pendingCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null && m.RetryCount < 10, ct);

            var failedCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null && m.RetryCount >= 10, ct);

            var oldestPending = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var processedLast24H = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt != null
                    && m.ProcessedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            return Results.Ok(new
            {
                pendingCount,
                failedCount,
                oldestPendingAge = oldestPending != default
                    ? (DateTimeOffset.UtcNow - oldestPending).TotalSeconds
                    : 0,
                processedLast24H,
                status = failedCount > 0 ? "degraded" : pendingCount > 100 ? "warning" : "healthy"
            });
        })
        .WithSummary("Get outbox status")
        .WithDescription("Returns outbox queue metrics for monitoring.");

        group.MapPost("/retry-failed", async (OutboxDbContext dbContext, CancellationToken ct) =>
        {
            var failed = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount >= 10)
                .ToListAsync(ct);

            foreach (var msg in failed)
            {
                msg.ResetForRetry();
            }

            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new { retriedCount = failed.Count });
        })
        .WithSummary("Retry failed outbox messages")
        .WithDescription("Resets retry count on failed messages so they are picked up by the processor again.");
    }
}
