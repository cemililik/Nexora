namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Integration event — published to Kafka (via Dapr pub/sub) for cross-module communication.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Unique identifier for this event instance.</summary>
    Guid EventId { get; }

    /// <summary>The UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>The tenant this event belongs to.</summary>
    string TenantId { get; }
}

/// <summary>
/// Base record for integration events with automatic ID, timestamp, and tenant context.
/// </summary>
public abstract record IntegrationEventBase : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public required string TenantId { get; init; }
}
