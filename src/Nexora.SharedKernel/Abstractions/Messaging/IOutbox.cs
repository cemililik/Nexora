using Nexora.SharedKernel.Domain.Events;

namespace Nexora.SharedKernel.Abstractions.Messaging;

/// <summary>
/// Enqueues integration events into the transactional outbox for reliable publishing.
/// Domain event handlers use this instead of <see cref="IEventBus"/> to guarantee
/// that events are persisted before being dispatched to the message broker.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Serializes and persists the integration event to the outbox table.
    /// The <see cref="OutboxProcessor"/> background service will later pick it up
    /// and publish it via <see cref="IEventBus"/>.
    /// </summary>
    Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}
