namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Detail DTO for notification with recipients.</summary>
public sealed record NotificationDetailDto(
    Guid Id,
    Guid? TemplateId,
    string Channel,
    string Subject,
    string BodyRendered,
    string Status,
    string TriggeredBy,
    Guid? TriggeredByUserId,
    int TotalRecipients,
    int DeliveredCount,
    int FailedCount,
    int OpenedCount,
    int ClickedCount,
    DateTime QueuedAt,
    DateTime? SentAt,
    IReadOnlyList<NotificationRecipientDto> Recipients);
