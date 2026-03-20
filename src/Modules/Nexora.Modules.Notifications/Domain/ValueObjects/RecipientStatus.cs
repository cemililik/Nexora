namespace Nexora.Modules.Notifications.Domain.ValueObjects;

/// <summary>Delivery status for an individual notification recipient.</summary>
public enum RecipientStatus
{
    Pending,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Failed,
    Unsubscribed
}
