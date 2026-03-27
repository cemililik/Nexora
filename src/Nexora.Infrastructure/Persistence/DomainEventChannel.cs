using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Bounded channel for domain events queued by synchronous SaveChanges callers.
/// Registered as a singleton so the dispatcher and background processor share the same instance.
/// Capacity is configurable via <see cref="DomainEventChannelOptions"/>.
/// </summary>
public sealed class DomainEventChannel(IOptions<DomainEventChannelOptions> options, ILogger<DomainEventChannel> logger)
{
    private readonly Channel<IDomainEvent> _channel = Channel.CreateBounded<IDomainEvent>(
        new BoundedChannelOptions(options.Value.Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

    /// <summary>Attempts to write an event. Returns false when the channel is at capacity or completed.</summary>
    public bool TryWrite(IDomainEvent domainEvent)
    {
        var written = _channel.Writer.TryWrite(domainEvent);
        if (!written)
        {
            if (_channel.Reader.Completion.IsCompleted)
                logger.LogWarning("Domain event channel completed, event {EventType} dropped", domainEvent.GetType().Name);
            else
                logger.LogWarning("Domain event channel at capacity, event {EventType} dropped", domainEvent.GetType().Name);
        }

        return written;
    }

    /// <summary>Reads all events asynchronously. Completes when the channel is closed or cancelled.</summary>
    public IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
