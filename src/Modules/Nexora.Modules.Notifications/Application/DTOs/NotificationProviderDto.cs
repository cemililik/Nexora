namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Summary DTO for notification provider (config masked for security).</summary>
public sealed record NotificationProviderDto(
    Guid Id,
    string Channel,
    string ProviderName,
    bool IsDefault,
    bool IsActive,
    int DailyLimit,
    int SentToday,
    DateTimeOffset CreatedAt);
