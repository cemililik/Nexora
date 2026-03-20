namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Summary DTO for sent notification list views.</summary>
public sealed record NotificationDto(
    Guid Id,
    string Channel,
    string Subject,
    string Status,
    string TriggeredBy,
    int TotalRecipients,
    int DeliveredCount,
    int FailedCount,
    DateTime QueuedAt,
    DateTime? SentAt);
