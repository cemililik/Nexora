namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a notification has been sent to all recipients.</summary>
public sealed record NotificationSentIntegrationEvent : IntegrationEventBase
{
    public required Guid NotificationId { get; init; }
    public required string Channel { get; init; }
    public required int RecipientCount { get; init; }
}
