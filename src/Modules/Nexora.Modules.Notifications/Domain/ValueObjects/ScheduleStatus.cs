namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Status of a scheduled notification.</summary>
public enum ScheduleStatus
{
    Pending,
    Dispatched,
    Cancelled
}
