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

        foreach (var domainEvent in domainEvents)
            await publisher.Publish(domainEvent, ct);
    }
}
