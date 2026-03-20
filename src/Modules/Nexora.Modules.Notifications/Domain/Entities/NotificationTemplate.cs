using Nexora.Modules.Notifications.Domain.Events;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Notifications.Domain.Entities;

/// <summary>
/// Aggregate root representing a notification template.
/// Templates define the content structure for notifications across channels.
/// Organization-scoped (nullable — system templates have no org).
/// </summary>
public sealed class NotificationTemplate : AuditableEntity<NotificationTemplateId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Module { get; private set; } = default!;
    public NotificationChannel Channel { get; private set; }
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public TemplateFormat Format { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }

    private readonly List<NotificationTemplateTranslation> _translations = [];
    public IReadOnlyList<NotificationTemplateTranslation> Translations => _translations.AsReadOnly();

    private NotificationTemplate() { }

    /// <summary>Creates a new notification template with the specified parameters.</summary>
    public static NotificationTemplate Create(
        Guid tenantId,
        string code,
        string module,
        NotificationChannel channel,
        string subject,
        string body,
        TemplateFormat format,
        bool isSystem = false,
        Guid? organizationId = null)
    {
        var template = new NotificationTemplate
        {
            Id = NotificationTemplateId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Code = code.Trim().ToLowerInvariant(),
            Module = module.Trim().ToLowerInvariant(),
            Channel = channel,
            Subject = subject.Trim(),
            Body = body,
            Format = format,
            IsSystem = isSystem,
            IsActive = true
        };
        template.AddDomainEvent(new TemplateCreatedEvent(template.Id, template.Code, template.Channel));
        return template;
    }

    /// <summary>Updates the template subject, body, and format. System templates cannot be edited.</summary>
    public void Update(string subject, string body, TemplateFormat format)
    {
        if (IsSystem)
            throw new DomainException("lockey_notifications_error_cannot_edit_system_template");

        Subject = subject.Trim();
        Body = body;
        Format = format;
        AddDomainEvent(new TemplateUpdatedEvent(Id, Code));
    }

    /// <summary>Activates the template so it can be used for notifications.</summary>
    public void Activate()
    {
        if (IsActive)
            throw new DomainException("lockey_notifications_error_template_already_active");

        IsActive = true;
    }

    /// <summary>Deactivates the template, preventing it from being used.</summary>
    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("lockey_notifications_error_template_already_inactive");

        IsActive = false;
    }

    /// <summary>Adds a translated version of the template for the specified language.</summary>
    public void AddTranslation(string languageCode, string subject, string body)
    {
        var existing = _translations.Find(t =>
            t.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            throw new DomainException("lockey_notifications_error_translation_already_exists");

        var translation = NotificationTemplateTranslation.Create(Id, languageCode, subject, body);
        _translations.Add(translation);
    }

    /// <summary>Updates an existing translation for the specified language.</summary>
    public void UpdateTranslation(string languageCode, string subject, string body)
    {
        var translation = _translations.Find(t =>
            t.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("lockey_notifications_error_translation_not_found");

        translation.Update(subject, body);
    }
}
