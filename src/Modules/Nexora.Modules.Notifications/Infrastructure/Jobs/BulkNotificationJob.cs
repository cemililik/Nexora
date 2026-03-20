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
        var tenantId = Guid.Parse(parameters.TenantId);

        var notification = await DeliveryJobHelper.LoadNotificationAsync(
            dbContext, parameters.NotificationId, tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for bulk delivery", parameters.NotificationId);
            return;
        }

        var provider = await DeliveryJobHelper.FindDefaultProviderAsync(
            dbContext, tenantId, notification.Channel, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default provider found for channel {Channel} in tenant {TenantId}",
                notification.Channel, tenantId);
            await DeliveryJobHelper.FailAllPendingAsync(
                dbContext, notification, "lockey_notifications_error_no_active_provider", ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

        var anySucceeded = false;
        var allSucceeded = true;
        var processedCount = 0;

        foreach (var batch in pendingRecipients.Chunk(parameters.BatchSize))
        {
            var (batchAny, batchAll) = DeliveryJobHelper.ProcessRecipients(
                batch, provider, "bulk_", logger);

            anySucceeded |= batchAny;
            allSucceeded &= batchAll;
            processedCount += batch.Count(r => r.Status == RecipientStatus.Sent);

            // Save after each batch to avoid large transactions
            await dbContext.SaveChangesAsync(ct);
        }

        DeliveryJobHelper.FinalizeNotificationStatus(notification, anySucceeded, allSucceeded);
        DeliveryJobHelper.UpdateNotificationCounts(notification);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Bulk notification {NotificationId} processed: {ProcessedCount}/{TotalCount} recipients for tenant {TenantId}",
            parameters.NotificationId, processedCount, pendingRecipients.Count, tenantId);
    }
}
