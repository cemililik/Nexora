using Microsoft.Extensions.Options;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence;

public sealed class DomainEventChannelTests
{
    [Fact]
    public void TryWrite_WithCapacityAvailable_ReturnsTrue()
    {
        var channel = CreateChannel(capacity: 100);
        var result = channel.TryWrite(new TestDomainEvent("A"));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryWrite_WhenFull_DropWriteDropsOldestAndReturnsTrue()
    {
        // DropWrite mode accepts the write but drops an item to make room
        var channel = CreateChannel(capacity: 2);

        channel.TryWrite(new TestDomainEvent("1")).Should().BeTrue();
        channel.TryWrite(new TestDomainEvent("2")).Should().BeTrue();
        channel.TryWrite(new TestDomainEvent("3")).Should().BeTrue(); // Accepted, oldest dropped

        // Only 2 events should be readable (capacity is 2)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var events = new List<IDomainEvent>();
        await foreach (var e in channel.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count == 2) break;
        }
        events.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadAllAsync_ReturnsQueuedEvents()
    {
        var channel = CreateChannel(capacity: 100);
        channel.TryWrite(new TestDomainEvent("X"));
        channel.TryWrite(new TestDomainEvent("Y"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var events = new List<IDomainEvent>();
        await foreach (var e in channel.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            if (events.Count == 2) break;
        }

        events.Should().HaveCount(2);
        events.Cast<TestDomainEvent>().Select(e => e.Name).Should().ContainInOrder("X", "Y");
    }

    [Fact]
    public void DefaultCapacity_Is10000()
    {
        var options = new DomainEventChannelOptions();
        options.Capacity.Should().Be(10_000);
    }

    private static DomainEventChannel CreateChannel(int capacity) =>
        new(Options.Create(new DomainEventChannelOptions { Capacity = capacity }));
}
