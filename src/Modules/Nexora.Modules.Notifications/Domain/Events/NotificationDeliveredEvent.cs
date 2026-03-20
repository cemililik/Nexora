using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Domain.Events;

/// <summary>Raised when a provider confirms delivery to a recipient.</summary>
public sealed record NotificationDeliveredEvent(NotificationId NotificationId, NotificationRecipientId RecipientId, Guid ContactId) : DomainEventBase;
