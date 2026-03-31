namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a notification needs to be delivered via its configured channel.</summary>
public sealed record NotificationDeliveryRequestedIntegrationEvent : IntegrationEventBase
{
    public required Guid NotificationId { get; init; }
    public required string Channel { get; init; }
    public required bool IsBulk { get; init; }
}
