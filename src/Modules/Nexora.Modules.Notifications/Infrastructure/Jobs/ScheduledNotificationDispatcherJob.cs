using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for the scheduled notification dispatcher job.</summary>
public sealed record ScheduledNotificationDispatcherJobParams : JobParams;

/// <summary>
/// Recurring job that dispatches pending scheduled notifications whose scheduled time has passed.
/// Marks schedules as dispatched and transitions notifications to Sending status for pickup by delivery jobs.
/// </summary>
public sealed class ScheduledNotificationDispatcherJob(
    ITenantContextAccessor tenantContextAccessor,
    NotificationsDbContext dbContext,
    ILogger<ScheduledNotificationDispatcherJob> logger) : NexoraJob<ScheduledNotificationDispatcherJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(ScheduledNotificationDispatcherJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);

        var dueSchedules = await (from s in dbContext.NotificationSchedules
                                  join n in dbContext.Notifications on s.NotificationId equals n.Id
                                  where n.TenantId == tenantId
                                        && s.Status == ScheduleStatus.Pending
                                        && s.ScheduledAt <= DateTime.UtcNow
                                  select s)
            .ToListAsync(ct);

        if (dueSchedules.Count == 0)
        {
            logger.LogDebug("No pending scheduled notifications due for tenant {TenantId}", tenantId);
            return;
        }

        var dispatchedCount = 0;

        foreach (var schedule in dueSchedules)
        {
            schedule.Dispatch();

            var notification = await dbContext.Notifications
                .FirstOrDefaultAsync(n => n.Id == schedule.NotificationId, ct);

            if (notification is null)
            {
                logger.LogWarning("Notification {NotificationId} for schedule {ScheduleId} not found",
                    schedule.NotificationId, schedule.Id);
                continue;
            }

            notification.MarkSending();
            dispatchedCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Dispatched {Count} scheduled notifications for tenant {TenantId}",
            dispatchedCount, tenantId);
    }
}
