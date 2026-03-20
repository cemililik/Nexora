using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for an email delivery job.</summary>
public sealed record EmailDeliveryJobParams : JobParams
{
    public required Guid NotificationId { get; init; }
}

/// <summary>
/// Background job that delivers a notification via the configured email provider.
/// Processes all pending recipients and updates their status.
/// </summary>
public sealed class EmailDeliveryJob(
    ITenantContextAccessor tenantContextAccessor,
    NotificationsDbContext dbContext,
    ILogger<EmailDeliveryJob> logger) : NexoraJob<EmailDeliveryJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(EmailDeliveryJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);

        var notification = await DeliveryJobHelper.LoadNotificationAsync(
            dbContext, parameters.NotificationId, tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for email delivery", parameters.NotificationId);
            return;
        }

        var provider = await DeliveryJobHelper.FindDefaultProviderAsync(
            dbContext, tenantId, NotificationChannel.Email, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default email provider found for tenant {TenantId}", tenantId);
            await DeliveryJobHelper.FailAllPendingAsync(
                dbContext, notification, "lockey_notifications_error_no_active_email_provider", ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

        var (anySucceeded, allSucceeded) = DeliveryJobHelper.ProcessRecipients(
            pendingRecipients, provider, "msg_", logger);

        DeliveryJobHelper.FinalizeNotificationStatus(notification, anySucceeded, allSucceeded);
        DeliveryJobHelper.UpdateNotificationCounts(notification);

        await dbContext.SaveChangesAsync(ct);
    }
}
