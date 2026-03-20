namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Lifecycle status of a notification.</summary>
public enum NotificationStatus
{
    Queued,
    Sending,
    Sent,
    PartialFailure,
    Failed
}
