using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Infrastructure-level recurring job that deletes processed outbox messages
/// older than the configured retention period. Runs against the public schema
/// outbox_messages table — not tenant-scoped.
/// </summary>
public sealed class OutboxCleanupJob(
    OutboxDbContext dbContext,
    IOptions<OutboxOptions> options,
    ILogger<OutboxCleanupJob> logger)
{
    /// <summary>
    /// Entry point called by Hangfire. Deletes processed outbox messages
    /// older than <see cref="OutboxOptions.CleanupAfterDays"/> days.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.CleanupAfterDays);

        logger.LogInformation(
            "OutboxCleanupJob starting — deleting processed messages older than {CutoffDate}",
            cutoff);

        var deletedCount = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation(
            "OutboxCleanupJob completed — deleted {DeletedCount} processed outbox messages",
            deletedCount);
    }
}
