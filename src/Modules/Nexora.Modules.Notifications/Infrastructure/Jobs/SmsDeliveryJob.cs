using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for an SMS delivery job.</summary>
public sealed record SmsDeliveryJobParams : JobParams
{
    public required Guid NotificationId { get; init; }
}

/// <summary>
/// Background job that delivers a notification via the configured SMS provider.
/// </summary>
public sealed class SmsDeliveryJob(
    ITenantContextAccessor tenantContextAccessor,
    NotificationsDbContext dbContext,
    ILogger<SmsDeliveryJob> logger) : NexoraJob<SmsDeliveryJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(SmsDeliveryJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);

        var notification = await DeliveryJobHelper.LoadNotificationAsync(
            dbContext, parameters.NotificationId, tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for SMS delivery", parameters.NotificationId);
            return;
        }

        var provider = await DeliveryJobHelper.FindDefaultProviderAsync(
            dbContext, tenantId, NotificationChannel.Sms, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default SMS provider found for tenant {TenantId}", tenantId);
            await DeliveryJobHelper.FailAllPendingAsync(
                dbContext, notification, "lockey_notifications_error_no_active_sms_provider", ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

        var (anySucceeded, allSucceeded) = DeliveryJobHelper.ProcessRecipients(
            pendingRecipients, provider, "sms_", logger);

        DeliveryJobHelper.FinalizeNotificationStatus(notification, anySucceeded, allSucceeded);
        DeliveryJobHelper.UpdateNotificationCounts(notification);

        await dbContext.SaveChangesAsync(ct);
    }
}
