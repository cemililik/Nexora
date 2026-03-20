namespace Nexora.Modules.Notifications.Application.DTOs;

/// <summary>DTO for notification template translation.</summary>
public sealed record NotificationTemplateTranslationDto(
    Guid Id,
    string LanguageCode,
    string Subject,
    string Body);
