using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for a bulk notification delivery job.</summary>
public sealed record BulkNotificationJobParams : JobParams
{
    public required Guid NotificationId { get; init; }
    public int BatchSize { get; init; } = 100;
}

/// <summary>
/// Background job that delivers a bulk notification in batches.
/// Respects provider daily limits and processes recipients in configurable batch sizes.
/// </summary>
public sealed class BulkNotificationJob(
    ITenantContextAccessor tenantContextAccessor,
    NotificationsDbContext dbContext,
    ILogger<BulkNotificationJob> logger) : NexoraJob<BulkNotificationJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(BulkNotificationJobParams parameters, CancellationToken ct)
    {
        var notificationId = NotificationId.From(parameters.NotificationId);
        var tenantId = Guid.Parse(parameters.TenantId);

        var notification = await dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for bulk delivery", parameters.NotificationId);
            return;
        }

        var provider = await dbContext.NotificationProviders
            .FirstOrDefaultAsync(p => p.TenantId == tenantId &&
                                      p.Channel == notification.Channel &&
                                      p.IsActive && p.IsDefault, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default provider found for channel {Channel} in tenant {TenantId}",
                notification.Channel, tenantId);
            foreach (var r in notification.Recipients.Where(r => r.Status == RecipientStatus.Pending))
                r.MarkFailed("lockey_notifications_error_no_active_provider");
            notification.MarkFailed();
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

        var allSucceeded = true;
        var anySucceeded = false;
        var processedCount = 0;

        foreach (var batch in pendingRecipients.Chunk(parameters.BatchSize))
        {
            foreach (var recipient in batch)
            {
                if (!provider.HasDailyCapacity())
                {
                    recipient.MarkFailed("lockey_notifications_error_provider_daily_limit_exceeded");
                    allSucceeded = false;
                    continue;
                }

                // In production: call provider API
                var providerMessageId = $"bulk_{Guid.NewGuid():N}";
                recipient.MarkSent(providerMessageId);
                provider.IncrementSentToday();
                anySucceeded = true;
                processedCount++;
            }

            // Save after each batch to avoid large transactions
            await dbContext.SaveChangesAsync(ct);
        }

        if (anySucceeded && allSucceeded)
            notification.MarkSent();
        else if (anySucceeded)
            notification.MarkPartialFailure();
        else
            notification.MarkFailed();

        notification.UpdateCounts(0, notification.Recipients.Count(r => r.Status is RecipientStatus.Failed or RecipientStatus.Bounced), 0, 0);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Bulk notification {NotificationId} processed: {ProcessedCount}/{TotalCount} recipients for tenant {TenantId}",
            parameters.NotificationId, processedCount, pendingRecipients.Count, tenantId);
    }
}
