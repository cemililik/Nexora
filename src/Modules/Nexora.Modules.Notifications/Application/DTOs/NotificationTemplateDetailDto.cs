namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>Detail DTO for notification template with translations.</summary>
public sealed record NotificationTemplateDetailDto(
    Guid Id,
    string Code,
    string Module,
    string Channel,
    string Subject,
    string Body,
    string Format,
    bool IsSystem,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NotificationTemplateTranslationDto> Translations);
