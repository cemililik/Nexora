using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when a notification is queued for delivery.</summary>
public sealed record NotificationQueuedEvent(NotificationId NotificationId, NotificationChannel Channel) : DomainEventBase;
