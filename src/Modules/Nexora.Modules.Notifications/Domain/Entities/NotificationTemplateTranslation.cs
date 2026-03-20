using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Translated content for a notification template in a specific language.
/// </summary>
public sealed class NotificationTemplateTranslation : AuditableEntity<NotificationTemplateTranslationId>
{
    public NotificationTemplateId TemplateId { get; private set; }
    public string LanguageCode { get; private set; } = default!;
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;

    private NotificationTemplateTranslation() { }

    /// <summary>Creates a new template translation for the given language.</summary>
    public static NotificationTemplateTranslation Create(
        NotificationTemplateId templateId,
        string languageCode,
        string subject,
        string body)
    {
        return new NotificationTemplateTranslation
        {
            Id = NotificationTemplateTranslationId.New(),
            TemplateId = templateId,
            LanguageCode = languageCode.Trim().ToLowerInvariant(),
            Subject = subject.Trim(),
            Body = body
        };
    }

    /// <summary>Updates the translated subject and body content.</summary>
    public void Update(string subject, string body)
    {
        Subject = subject.Trim();
        Body = body;
    }
}
