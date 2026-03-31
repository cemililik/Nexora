using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for the scheduled notification dispatcher job.</summary>
public sealed record ScheduledNotificationDispatcherJobParams : JobParams;

/// <summary>
/// Recurring job that dispatches pending scheduled notifications whose scheduled time has passed.
/// Marks schedules as dispatched and transitions notifications to Sending status for pickup by delivery jobs.
/// </summary>
public sealed class ScheduledNotificationDispatcherJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledNotificationDispatcherJob> logger) : PlatformJob<ScheduledNotificationDispatcherJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "notifications";

    protected override async Task ExecuteForTenantAsync(
        ScheduledNotificationDispatcherJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<NotificationsDbContext>();

        var dueSchedules = await (from s in dbContext.NotificationSchedules
                                  where s.Status == ScheduleStatus.Pending
                                        && s.ScheduledAt <= DateTime.UtcNow
                                  select s)
            .ToListAsync(ct);

        if (dueSchedules.Count == 0)
        {
            logger.LogDebug("No pending scheduled notifications due");
            return;
        }

        var outbox = scopedServices.GetRequiredService<IOutbox>();
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

            var deliveryEvent = new NotificationDeliveryRequestedIntegrationEvent
            {
                TenantId = tenant.TenantId,
                NotificationId = notification.Id.Value,
                Channel = notification.Channel.ToString(),
                IsBulk = notification.TriggeredBy == TriggerSource.BulkApi
            };

            await outbox.EnqueueAsync(deliveryEvent, ct);
            dispatchedCount++;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Dispatched {Count} scheduled notifications via outbox", dispatchedCount);
    }
}
