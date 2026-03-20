namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a notification is confirmed delivered to a recipient.</summary>
public sealed record NotificationDeliveredIntegrationEvent : IntegrationEventBase
{
    public required Guid NotificationId { get; init; }
    public required Guid RecipientId { get; init; }
    public required Guid ContactId { get; init; }
}
