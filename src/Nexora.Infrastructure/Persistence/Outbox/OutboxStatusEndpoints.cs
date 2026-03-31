using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Results;

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

        group.MapGet("/status", async (OutboxDbContext dbContext, IOptions<OutboxOptions> options, CancellationToken ct) =>
        {
            var maxRetryCount = options.Value.MaxRetryCount;
            var degradedThreshold = options.Value.DegradedThreshold;
            var unhealthyThreshold = options.Value.UnhealthyThreshold;

            // All unprocessed messages regardless of retry count
            var totalPendingCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null, ct);

            // Retryable: not yet at max retries
            var retryablePendingCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null && m.RetryCount < maxRetryCount, ct);

            // Dead-lettered: exhausted all retries
            var failedCount = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt == null && m.RetryCount >= maxRetryCount, ct);

            var oldestPending = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.CreatedAt)
                .FirstOrDefaultAsync(ct);

            var processedLast24H = await dbContext.OutboxMessages
                .CountAsync(m => m.ProcessedAt != null
                    && m.ProcessedAt >= DateTimeOffset.UtcNow.AddHours(-24), ct);

            string statusKey;
            if (totalPendingCount >= unhealthyThreshold || failedCount > 0)
                statusKey = "lockey_outbox_status_degraded";
            else if (totalPendingCount >= degradedThreshold)
                statusKey = "lockey_outbox_status_warning";
            else
                statusKey = "lockey_outbox_status_healthy";

            var dto = new OutboxStatusDto(
                PendingCount: totalPendingCount,
                RetryablePendingCount: retryablePendingCount,
                FailedCount: failedCount,
                OldestPendingAgeSeconds: oldestPending != default
                    ? (DateTimeOffset.UtcNow - oldestPending).TotalSeconds
                    : 0,
                ProcessedLast24H: processedLast24H,
                StatusKey: statusKey);

            return Results.Ok(ApiEnvelope<OutboxStatusDto>.Success(dto));
        })
        .WithSummary("Get outbox status")
        .WithDescription("Returns outbox queue metrics for monitoring.");

        group.MapPost("/retry-failed", async (OutboxDbContext dbContext, IOptions<OutboxOptions> options, CancellationToken ct) =>
        {
            var maxRetryCount = options.Value.MaxRetryCount;

            var failed = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.RetryCount >= maxRetryCount)
                .ToListAsync(ct);

            foreach (var msg in failed)
            {
                msg.ResetForRetry();
            }

            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(ApiEnvelope<OutboxRetryResultDto>.Success(new OutboxRetryResultDto(failed.Count)));
        })
        .WithSummary("Retry failed outbox messages")
        .WithDescription("Resets retry count on failed messages so they are picked up by the processor again.");
    }
}

/// <summary>Outbox queue status snapshot.</summary>
public sealed record OutboxStatusDto(
    int PendingCount,
    int RetryablePendingCount,
    int FailedCount,
    double OldestPendingAgeSeconds,
    int ProcessedLast24H,
    string StatusKey);

/// <summary>Result of a bulk retry operation.</summary>
public sealed record OutboxRetryResultDto(int RetriedCount);
