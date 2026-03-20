namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when an email bounces for a contact.</summary>
public sealed record NotificationBouncedIntegrationEvent : IntegrationEventBase
{
    public required Guid NotificationId { get; init; }
    public required Guid ContactId { get; init; }
    public required string Email { get; init; }
}
