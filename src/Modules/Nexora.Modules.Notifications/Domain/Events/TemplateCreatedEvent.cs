using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when a new notification template is created.</summary>
public sealed record TemplateCreatedEvent(NotificationTemplateId TemplateId, string Code, NotificationChannel Channel) : DomainEventBase;
