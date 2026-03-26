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

/// <summary>
/// Bounded channel for domain events queued by synchronous SaveChanges callers.
/// Registered as a singleton so the dispatcher and background processor share the same instance.
/// </summary>
public sealed class DomainEventChannel
{
    private const int Capacity = 10_000;

    private readonly Channel<IDomainEvent> _channel = Channel.CreateBounded<IDomainEvent>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropWrite
        });

    /// <summary>Attempts to write an event. Returns false if the channel is full or completed.</summary>
    public bool TryWrite(IDomainEvent domainEvent) => _channel.Writer.TryWrite(domainEvent);

    public IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Background service that processes domain events queued via <see cref="DomainEventChannel"/>.
/// Retries transient failures before logging and dropping the event.
/// </summary>
public sealed class DomainEventBackgroundProcessor(
    DomainEventChannel channel,
    IPublisher publisher,
    ILogger<DomainEventBackgroundProcessor> logger) : BackgroundService
{
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in channel.ReadAllAsync(stoppingToken))
        {
            var eventType = domainEvent.GetType().Name;

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await publisher.Publish(domainEvent, stoppingToken);
                    break;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
                    logger.LogWarning(ex,
                        "Domain event {EventType} dispatch failed (attempt {Attempt}/{MaxRetries}), retrying in {DelayMs}ms",
                        eventType, attempt, MaxRetries, delay.TotalMilliseconds);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Domain event {EventType} dispatch failed after {MaxRetries} attempts — event dropped",
                        eventType, MaxRetries);
                }
            }
        }
    }
}
