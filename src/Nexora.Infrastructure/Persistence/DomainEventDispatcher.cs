using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Dispatches domain events collected from entities after SaveChanges.
/// Async callers dispatch inline; sync callers queue events for background processing
/// to avoid sync-over-async deadlocks.
/// </summary>
public sealed class DomainEventDispatcher(
    IPublisher publisher,
    DomainEventChannel channel,
    ILogger<DomainEventDispatcher> logger)
{
    /// <summary>Collects and publishes domain events from tracked entities, then clears them.</summary>
    public async Task DispatchEventsAsync(DbContext context, CancellationToken ct)
    {
        var domainEvents = CollectAndClearEvents(context);

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, ct);
    }

    /// <summary>
    /// Queues domain events for background dispatch. Safe to call from synchronous code
    /// because it never blocks on async work. Failures during shutdown are logged and swallowed
    /// so that a completed channel does not surface as a persistence error.
    /// </summary>
    public void DispatchEvents(DbContext context)
    {
        var domainEvents = CollectAndClearEvents(context);

        foreach (var domainEvent in domainEvents)
        {
            if (!channel.TryWrite(domainEvent))
            {
                logger.LogWarning(
                    "Failed to queue domain event {EventType} — channel is full or completed (shutdown). Event will not be dispatched",
                    domainEvent.GetType().Name);
            }
        }
    }

    private static List<IDomainEvent> CollectAndClearEvents(DbContext context)
    {
        var entities = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IHasDomainEvents)
            .Select(e => (IHasDomainEvents)e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
            entity.ClearDomainEvents();

        return domainEvents;
    }
}
