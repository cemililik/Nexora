using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Represents a scheduled notification pending dispatch at a specific time.
/// </summary>
public sealed class NotificationSchedule : AuditableEntity<NotificationScheduleId>
{
    public NotificationId NotificationId { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public ScheduleStatus Status { get; private set; }

    private NotificationSchedule() { }

    /// <summary>Creates a new schedule for dispatching a notification at a future time.</summary>
    public static NotificationSchedule Create(
        NotificationId notificationId,
        DateTime scheduledAt)
    {
        if (scheduledAt <= DateTime.UtcNow)
            throw new DomainException("lockey_notifications_error_schedule_must_be_future");

        return new NotificationSchedule
        {
            Id = NotificationScheduleId.New(),
            NotificationId = notificationId,
            ScheduledAt = scheduledAt,
            Status = ScheduleStatus.Pending
        };
    }

    /// <summary>Marks the schedule as dispatched, triggering notification delivery.</summary>
    public void Dispatch()
    {
        if (Status is not ScheduleStatus.Pending)
            throw new DomainException("lockey_notifications_error_schedule_not_pending");

        Status = ScheduleStatus.Dispatched;
    }

    /// <summary>Cancels the pending scheduled notification.</summary>
    public void Cancel()
    {
        if (Status is not ScheduleStatus.Pending)
            throw new DomainException("lockey_notifications_error_schedule_not_pending");

        Status = ScheduleStatus.Cancelled;
    }
}
