using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for the notification cleanup/archival job.</summary>
public sealed record NotificationCleanupJobParams : JobParams
{
    /// <summary>Number of days to retain notifications before cleanup.</summary>
    public int RetentionDays { get; init; } = 90;
}

/// <summary>
/// Recurring maintenance job that cleans up old notification records.
/// Deletes notifications older than the configured retention period.
/// </summary>
public sealed class NotificationCleanupJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationCleanupJob> logger) : PlatformJob<NotificationCleanupJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "notifications";

    protected override async Task ExecuteForTenantAsync(
        NotificationCleanupJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<NotificationsDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-parameters.RetentionDays);

        var oldNotifications = await dbContext.Notifications
            .Where(n => n.QueuedAt < cutoffDate &&
                        n.Status != NotificationStatus.Sending)
            .ToListAsync(ct);

        if (oldNotifications.Count == 0)
        {
            logger.LogInformation("No notifications older than {RetentionDays} days found",
                parameters.RetentionDays);
            return;
        }

        dbContext.Notifications.RemoveRange(oldNotifications);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Cleaned up {Count} old notifications (older than {RetentionDays} days)",
            oldNotifications.Count, parameters.RetentionDays);
    }
}
