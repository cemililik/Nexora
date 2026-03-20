using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when notification delivery fails completely.</summary>
public sealed record NotificationFailedEvent(NotificationId NotificationId, NotificationChannel Channel) : DomainEventBase;
