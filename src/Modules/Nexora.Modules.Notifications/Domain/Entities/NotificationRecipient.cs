using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Tracks delivery status for an individual notification recipient.
/// </summary>
public sealed class NotificationRecipient : AuditableEntity<NotificationRecipientId>
{
    public NotificationId NotificationId { get; private set; }
    public Guid ContactId { get; private set; }
    public string RecipientAddress { get; private set; } = default!;
    public RecipientStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? OpenedAt { get; private set; }

    private NotificationRecipient() { }

    /// <summary>Creates a new recipient entry for the specified notification and contact.</summary>
    public static NotificationRecipient Create(
        NotificationId notificationId,
        Guid contactId,
        string recipientAddress)
    {
        return new NotificationRecipient
        {
            Id = NotificationRecipientId.New(),
            NotificationId = notificationId,
            ContactId = contactId,
            RecipientAddress = recipientAddress.Trim(),
            Status = RecipientStatus.Pending
        };
    }

    /// <summary>Marks the recipient as sent and records the provider message identifier.</summary>
    public void MarkSent(string providerMessageId)
    {
        if (Status is not RecipientStatus.Pending)
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Sent;
        ProviderMessageId = providerMessageId;
        SentAt = DateTime.UtcNow;
    }

    /// <summary>Marks the recipient as successfully delivered.</summary>
    public void MarkDelivered()
    {
        if (Status is not RecipientStatus.Sent)
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
    }

    /// <summary>Marks the recipient as having opened the notification.</summary>
    public void MarkOpened()
    {
        if (Status is not (RecipientStatus.Delivered or RecipientStatus.Sent))
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Opened;
        OpenedAt = DateTime.UtcNow;
    }

    /// <summary>Marks the recipient as having clicked a link in the notification.</summary>
    public void MarkClicked()
    {
        if (Status is not (RecipientStatus.Opened or RecipientStatus.Delivered))
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Clicked;
    }

    /// <summary>Marks the recipient as bounced with the specified reason.</summary>
    public void MarkBounced(string reason)
    {
        if (Status is not (RecipientStatus.Sent or RecipientStatus.Pending))
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Bounced;
        FailureReason = reason;
    }

    /// <summary>Marks the recipient as failed with the specified reason.</summary>
    public void MarkFailed(string reason)
    {
        if (Status is not (RecipientStatus.Pending or RecipientStatus.Sent))
            throw new DomainException("lockey_notifications_error_recipient_invalid_transition");

        Status = RecipientStatus.Failed;
        FailureReason = reason;
    }
}
