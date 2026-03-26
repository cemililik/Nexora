using System.Threading.Channels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Dispatches domain events collected from entities after SaveChanges.
/// Async callers dispatch inline; sync callers queue events for background processing
/// to avoid sync-over-async deadlocks.
/// </summary>
public sealed class DomainEventDispatcher(IPublisher publisher, DomainEventChannel channel)
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
    /// because it never blocks on async work.
    /// </summary>
    public void DispatchEvents(DbContext context)
    {
        var domainEvents = CollectAndClearEvents(context);

        foreach (var domainEvent in domainEvents)
            channel.Write(domainEvent);
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

/// <summary>
/// Unbounded channel for domain events queued by synchronous SaveChanges callers.
/// Registered as a singleton so the dispatcher and background processor share the same instance.
/// </summary>
public sealed class DomainEventChannel
{
    private readonly Channel<IDomainEvent> _channel = Channel.CreateUnbounded<IDomainEvent>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Write(IDomainEvent domainEvent)
    {
        if (!_channel.Writer.TryWrite(domainEvent))
            throw new InvalidOperationException("Failed to queue domain event for background dispatch.");
    }

    public IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Background service that processes domain events queued via <see cref="DomainEventChannel"/>.
/// </summary>
public sealed class DomainEventBackgroundProcessor(
    DomainEventChannel channel,
    IPublisher publisher,
    ILogger<DomainEventBackgroundProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                await publisher.Publish(domainEvent, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch queued domain event {EventType}", domainEvent.GetType().Name);
            }
        }
    }
}
