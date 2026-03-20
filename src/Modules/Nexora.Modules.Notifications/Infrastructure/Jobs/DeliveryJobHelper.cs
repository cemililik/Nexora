using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>
/// Shared helper for delivery jobs. Handles provider lookup, capacity checking,
/// recipient iteration, and notification status finalization.
/// </summary>
public static class DeliveryJobHelper
{
    /// <summary>Loads a notification with its recipients by ID and tenant.</summary>
    public static async Task<Notification?> LoadNotificationAsync(
        NotificationsDbContext dbContext,
        Guid notificationGuid,
        Guid tenantId,
        CancellationToken ct)
    {
        var notificationId = NotificationId.From(notificationGuid);
        return await dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, ct);
    }

    /// <summary>Finds the active default provider for the given channel and tenant.</summary>
    public static async Task<NotificationProvider?> FindDefaultProviderAsync(
        NotificationsDbContext dbContext,
        Guid tenantId,
        NotificationChannel channel,
        CancellationToken ct)
    {
        return await dbContext.NotificationProviders
            .FirstOrDefaultAsync(p => p.TenantId == tenantId &&
                                      p.Channel == channel &&
                                      p.IsActive && p.IsDefault, ct);
    }

    /// <summary>
    /// Marks all pending recipients as failed and the notification as failed.
    /// Used when no active provider is available.
    /// </summary>
    public static async Task FailAllPendingAsync(
        NotificationsDbContext dbContext,
        Notification notification,
        string failureReason,
        CancellationToken ct)
    {
        foreach (var r in notification.Recipients.Where(r => r.Status == RecipientStatus.Pending))
            r.MarkFailed(failureReason);
        notification.MarkFailed();
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Processes pending recipients: checks provider capacity, marks sent with generated message ID.
    /// Returns (anySucceeded, allSucceeded) to determine final notification status.
    /// </summary>
    public static (bool AnySucceeded, bool AllSucceeded) ProcessRecipients(
        IList<NotificationRecipient> pendingRecipients,
        NotificationProvider provider,
        string messageIdPrefix,
        ILogger logger)
    {
        var allSucceeded = true;
        var anySucceeded = false;

        foreach (var recipient in pendingRecipients)
        {
            if (!provider.HasDailyCapacity())
            {
                recipient.MarkFailed("lockey_notifications_error_provider_daily_limit_exceeded");
                allSucceeded = false;
                continue;
            }

            // In production: call provider API (SendGrid, Twilio, etc.)
            var providerMessageId = $"{messageIdPrefix}{Guid.NewGuid():N}";
            recipient.MarkSent(providerMessageId);
            provider.IncrementSentToday();
            anySucceeded = true;

            logger.LogInformation("Message sent to {RecipientAddress} via {ProviderName}, messageId: {MessageId}",
                recipient.RecipientAddress, provider.ProviderName, providerMessageId);
        }

        return (anySucceeded, allSucceeded);
    }

    /// <summary>Finalizes notification status based on recipient processing results.</summary>
    public static void FinalizeNotificationStatus(
        Notification notification,
        bool anySucceeded,
        bool allSucceeded)
    {
        if (anySucceeded && allSucceeded)
            notification.MarkSent();
        else if (anySucceeded)
            notification.MarkPartialFailure();
        else
            notification.MarkFailed();
    }

    /// <summary>Recalculates and updates notification-level delivery counts.</summary>
    public static void UpdateNotificationCounts(Notification notification)
    {
        var delivered = notification.Recipients.Count(r => r.Status == RecipientStatus.Delivered);
        var failed = notification.Recipients.Count(r => r.Status is RecipientStatus.Failed or RecipientStatus.Bounced);
        var opened = notification.Recipients.Count(r => r.Status == RecipientStatus.Opened);
        var clicked = notification.Recipients.Count(r => r.Status == RecipientStatus.Clicked);
        notification.UpdateCounts(delivered, failed, opened, clicked);
    }
}
