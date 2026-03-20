using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when a recipient opens an email notification.</summary>
public sealed record NotificationOpenedEvent(NotificationId NotificationId, NotificationRecipientId RecipientId) : DomainEventBase;
