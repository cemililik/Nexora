using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when a notification template is updated.</summary>
public sealed record TemplateUpdatedEvent(NotificationTemplateId TemplateId, string Code) : DomainEventBase;
