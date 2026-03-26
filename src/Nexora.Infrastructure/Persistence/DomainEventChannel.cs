using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Bounded channel for domain events queued by synchronous SaveChanges callers.
/// Registered as a singleton so the dispatcher and background processor share the same instance.
/// Capacity is configurable via <see cref="DomainEventChannelOptions"/>.
/// </summary>
public sealed class DomainEventChannel
{
    private readonly Channel<IDomainEvent> _channel;

    public DomainEventChannel(IOptions<DomainEventChannelOptions> options)
    {
        var capacity = options.Value.Capacity;
        _channel = Channel.CreateBounded<IDomainEvent>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropWrite
            });
    }

    /// <summary>Attempts to write an event. Returns false if the channel is full or completed.</summary>
    public bool TryWrite(IDomainEvent domainEvent) => _channel.Writer.TryWrite(domainEvent);

    /// <summary>Reads all events asynchronously. Completes when the channel is closed or cancelled.</summary>
    public IAsyncEnumerable<IDomainEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
