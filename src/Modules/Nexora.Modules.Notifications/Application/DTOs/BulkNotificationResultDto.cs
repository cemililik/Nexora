namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Result DTO for bulk notification sending.</summary>
public sealed record BulkNotificationResultDto(
    Guid NotificationId,
    int TotalRecipients,
    int QueuedCount,
    int SkippedCount);
