using Nexora.SharedKernel.Domain.Events;

namespace Nexora.SharedKernel.Abstractions.Messaging;

/// <summary>
/// Publishes integration events to the message broker (Kafka via Dapr pub/sub).
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// Handles integration events from other modules.
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}
