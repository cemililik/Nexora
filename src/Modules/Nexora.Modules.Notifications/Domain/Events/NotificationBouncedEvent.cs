using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when an email bounces for a recipient.</summary>
public sealed record NotificationBouncedEvent(NotificationId NotificationId, Guid ContactId, string Email) : DomainEventBase;
