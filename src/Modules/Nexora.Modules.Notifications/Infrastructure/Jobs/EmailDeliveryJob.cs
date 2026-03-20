using Microsoft.EntityFrameworkCore;
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
        var notificationId = NotificationId.From(parameters.NotificationId);
        var tenantId = Guid.Parse(parameters.TenantId);

        var notification = await dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, ct);

        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found for email delivery", parameters.NotificationId);
            return;
        }

        var provider = await dbContext.NotificationProviders
            .FirstOrDefaultAsync(p => p.TenantId == tenantId &&
                                      p.Channel == NotificationChannel.Email &&
                                      p.IsActive && p.IsDefault, ct);

        if (provider is null)
        {
            logger.LogWarning("No active default email provider found for tenant {TenantId}", tenantId);
            foreach (var r in notification.Recipients.Where(r => r.Status == RecipientStatus.Pending))
                r.MarkFailed("lockey_notifications_error_no_active_email_provider");
            notification.MarkFailed();
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        var pendingRecipients = notification.Recipients
            .Where(r => r.Status == RecipientStatus.Pending)
            .ToList();

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

            // In production: call provider API (SendGrid, Mailgun, etc.)
            // For now, simulate successful send with a generated message ID
            var providerMessageId = $"msg_{Guid.NewGuid():N}";
            recipient.MarkSent(providerMessageId);
            provider.IncrementSentToday();
            anySucceeded = true;

            logger.LogInformation("Email sent to {RecipientAddress} via {ProviderName}, messageId: {MessageId}",
                recipient.RecipientAddress, provider.ProviderName, providerMessageId);
        }

        if (anySucceeded && allSucceeded)
            notification.MarkSent();
        else if (anySucceeded)
            notification.MarkPartialFailure();
        else
            notification.MarkFailed();

        var delivered = notification.Recipients.Count(r => r.Status == RecipientStatus.Delivered);
        var failed = notification.Recipients.Count(r => r.Status is RecipientStatus.Failed or RecipientStatus.Bounced);
        notification.UpdateCounts(delivered, failed, 0, 0);

        await dbContext.SaveChangesAsync(ct);
    }
}
