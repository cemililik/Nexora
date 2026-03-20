using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Aggregate root representing a sent or queued notification.
/// Tracks overall delivery status and recipient-level details.
/// </summary>
public sealed class Notification : AuditableEntity<NotificationId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public NotificationTemplateId? TemplateId { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Subject { get; private set; } = default!;
    public string BodyRendered { get; private set; } = default!;
    public NotificationStatus Status { get; private set; }
    public string TriggeredBy { get; private set; } = default!;
    public Guid? TriggeredByUserId { get; private set; }
    public int TotalRecipients { get; private set; }
    public int DeliveredCount { get; private set; }
    public int FailedCount { get; private set; }
    public int OpenedCount { get; private set; }
    public int ClickedCount { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    private readonly List<NotificationRecipient> _recipients = [];
    public IReadOnlyList<NotificationRecipient> Recipients => _recipients.AsReadOnly();

    private Notification() { }

    /// <summary>Creates a new queued notification with the specified content and channel.</summary>
    public static Notification Create(
        Guid tenantId,
        NotificationChannel channel,
        string subject,
        string bodyRendered,
        string triggeredBy,
        NotificationTemplateId? templateId = null,
        Guid? triggeredByUserId = null,
        Guid? organizationId = null)
    {
        var notification = new Notification
        {
            Id = NotificationId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            TemplateId = templateId,
            Channel = channel,
            Subject = subject.Trim(),
            BodyRendered = bodyRendered,
            Status = NotificationStatus.Queued,
            TriggeredBy = triggeredBy,
            TriggeredByUserId = triggeredByUserId,
            TotalRecipients = 0,
            DeliveredCount = 0,
            FailedCount = 0,
            OpenedCount = 0,
            ClickedCount = 0,
            QueuedAt = DateTime.UtcNow
        };
        notification.AddDomainEvent(new NotificationQueuedEvent(notification.Id, channel));
        return notification;
    }

    /// <summary>Adds a recipient to this notification and updates the total count.</summary>
    public NotificationRecipient AddRecipient(Guid contactId, string recipientAddress)
    {
        var recipient = NotificationRecipient.Create(Id, contactId, recipientAddress);
        _recipients.Add(recipient);
        TotalRecipients = _recipients.Count;
        return recipient;
    }

    /// <summary>Transitions the notification to the Sending status.</summary>
    public void MarkSending()
    {
        if (Status is not NotificationStatus.Queued)
            throw new DomainException("lockey_notifications_error_only_queued_can_send");

        Status = NotificationStatus.Sending;
    }

    /// <summary>Marks the notification as successfully sent.</summary>
    public void MarkSent()
    {
        if (Status is not (NotificationStatus.Sending or NotificationStatus.Queued))
            throw new DomainException("lockey_notifications_error_invalid_status_transition");

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        AddDomainEvent(new NotificationSentEvent(Id, Channel, TotalRecipients));
    }

    /// <summary>Marks the notification as partially failed when some recipients could not be reached.</summary>
    public void MarkPartialFailure()
    {
        if (Status is not NotificationStatus.Sending)
            throw new DomainException("lockey_notifications_error_invalid_status_transition");

        Status = NotificationStatus.PartialFailure;
        SentAt = DateTime.UtcNow;
    }

    /// <summary>Marks the notification as completely failed.</summary>
    public void MarkFailed()
    {
        if (Status is not (NotificationStatus.Sending or NotificationStatus.Queued))
            throw new DomainException("lockey_notifications_error_invalid_status_transition");

        Status = NotificationStatus.Failed;
        SentAt = DateTime.UtcNow;
        AddDomainEvent(new NotificationFailedEvent(Id, Channel));
    }

    /// <summary>Updates the delivery tracking counters for this notification.</summary>
    public void UpdateCounts(int delivered, int failed, int opened, int clicked)
    {
        DeliveredCount = delivered;
        FailedCount = failed;
        OpenedCount = opened;
        ClickedCount = clicked;
    }
}
