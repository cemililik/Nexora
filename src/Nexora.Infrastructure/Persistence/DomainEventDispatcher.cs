using MediatR;
using Microsoft.EntityFrameworkCore;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Dispatches domain events collected from entities after SaveChanges.
/// </summary>
public sealed class DomainEventDispatcher(IPublisher publisher)
{
    /// <summary>Collects and publishes domain events from tracked entities, then clears them.</summary>
    public async Task DispatchEventsAsync(DbContext context, CancellationToken ct)
    {
        var domainEvents = CollectAndClearEvents(context);

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, ct);
    }

    /// <summary>
    /// Synchronous dispatch that avoids deadlocks by running on a thread-pool thread.
    /// Use only from synchronous SaveChanges overloads.
    /// </summary>
    public void DispatchEvents(DbContext context)
    {
        var domainEvents = CollectAndClearEvents(context);

        if (domainEvents.Count == 0)
            return;

        Task.Run(async () =>
        {
            foreach (var domainEvent in domainEvents)
                await publisher.Publish(domainEvent, CancellationToken.None);
        }).GetAwaiter().GetResult();
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
