using Dapr.Client;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events to Kafka via Dapr Pub/Sub.
/// </summary>
public sealed class DaprEventBus(DaprClient daprClient) : IEventBus
{
    private const string PubSubName = "pubsub";

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        var topicName = typeof(TEvent).Name;
        await daprClient.PublishEventAsync(PubSubName, topicName, @event, ct);
    }
}
