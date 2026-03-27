using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence;

public sealed class DomainEventBackgroundProcessorTests
{
    private readonly DomainEventChannel _channel = new(
        Options.Create(new DomainEventChannelOptions { Capacity = 100 }));

    [Fact]
    public async Task ExecuteAsync_WithQueuedEvent_DispatchesEvent()
    {
        var publisher = Substitute.For<IPublisher>();
        var processor = CreateProcessor(publisher);

        _channel.TryWrite(new TestDomainEvent("E1"));

        await processor.StartAsync(CancellationToken.None);
        await processor.StopAsync(CancellationToken.None);

        await publisher.Received(1).Publish(
            Arg.Is<TestDomainEvent>(e => e.Name == "E1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientError_RetriesAndSucceeds()
    {
        var publisher = Substitute.For<IPublisher>();

        var callCount = 0;
        publisher.Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 1) throw new TimeoutException("transient");
                return Task.CompletedTask;
            });

        var processor = CreateProcessor(publisher);
        _channel.TryWrite(new TestDomainEvent("Retry"));

        await processor.StartAsync(CancellationToken.None);
        // Wait for retry backoff (100ms initial * 2^0 = 100ms) plus processing overhead.
        // StopAsync alone is insufficient here because the retry delay is in-flight
        // when StopAsync signals cancellation, and we need the second attempt to complete.
        await Task.Delay(500);
        await processor.StopAsync(CancellationToken.None);

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonTransientError_DoesNotRetry()
    {
        var publisher = Substitute.For<IPublisher>();

        var callCount = 0;
        publisher.Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                throw new InvalidOperationException("non-transient");
            });

        var processor = CreateProcessor(publisher);
        _channel.TryWrite(new TestDomainEvent("NoRetry"));

        await processor.StartAsync(CancellationToken.None);
        await processor.StopAsync(CancellationToken.None);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleEvents_CreatesOneScopePerEvent()
    {
        var (processor, publisher, scopeCount) = CreateProcessorWithScopeTracking();

        _channel.TryWrite(new TestDomainEvent("First"));
        _channel.TryWrite(new TestDomainEvent("Second"));
        _channel.TryWrite(new TestDomainEvent("Third"));

        await processor.StartAsync(CancellationToken.None);
        await processor.StopAsync(CancellationToken.None);

        scopeCount().Should().Be(3);
        await publisher.Received(3).Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    private DomainEventBackgroundProcessor CreateProcessor(IPublisher publisher)
    {
        var services = new ServiceCollection();
        services.AddSingleton(publisher);
        var sp = services.BuildServiceProvider();

        return new DomainEventBackgroundProcessor(
            _channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventBackgroundProcessor>.Instance);
    }

    private (DomainEventBackgroundProcessor Processor, IPublisher Publisher, Func<int> ScopeCount) CreateProcessorWithScopeTracking()
    {
        var publisher = Substitute.For<IPublisher>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var scopeCreatedCount = 0;

        scopeFactory.CreateScope().Returns(_ => { scopeCreatedCount++; return scope; });
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IPublisher)).Returns(publisher);

        var processor = new DomainEventBackgroundProcessor(
            _channel, scopeFactory, NullLogger<DomainEventBackgroundProcessor>.Instance);

        return (processor, publisher, () => scopeCreatedCount);
    }
}
