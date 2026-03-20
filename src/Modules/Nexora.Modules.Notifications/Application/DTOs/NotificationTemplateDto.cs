namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Summary DTO for notification template list views.</summary>
public sealed record NotificationTemplateDto(
    Guid Id,
    string Code,
    string Module,
    string Channel,
    string Subject,
    string Format,
    bool IsSystem,
    bool IsActive,
    DateTimeOffset CreatedAt);
