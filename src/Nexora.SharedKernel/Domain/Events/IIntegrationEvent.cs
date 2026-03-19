namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Integration event — published to Kafka (via Dapr pub/sub) for cross-module communication.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string TenantId { get; }
}

public abstract record IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public required string TenantId { get; init; }
}
