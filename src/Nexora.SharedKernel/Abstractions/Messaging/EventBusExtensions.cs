using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.SharedKernel.Abstractions.Messaging;

/// <summary>
/// Extension methods for <see cref="IEventBus"/> that standardize the publish-and-log pattern
/// used by domain-to-integration event handlers across modules.
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// Publishes an integration event and logs a standardized message.
    /// Centralizes the publish step so error handling, retries, or telemetry
    /// can be added in one place.
    /// </summary>
    public static async Task PublishAndLogAsync<TEvent>(
        this IEventBus eventBus,
        TEvent @event,
        ILogger logger,
        CancellationToken ct) where TEvent : IIntegrationEvent
    {
        await eventBus.PublishAsync(@event, ct);
        logger.LogInformation("Published {EventType} for tenant {TenantId} (EventId: {EventId})",
            typeof(TEvent).Name, @event.TenantId, @event.EventId);
    }
}
