using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when all recipients of a notification have been processed.</summary>
public sealed record NotificationSentEvent(NotificationId NotificationId, NotificationChannel Channel, int RecipientCount) : DomainEventBase;
