namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>DTO for scheduled notification status.</summary>
public sealed record NotificationScheduleDto(
    Guid Id,
    Guid NotificationId,
    DateTime ScheduledAt,
    string Status,
    DateTimeOffset CreatedAt);
