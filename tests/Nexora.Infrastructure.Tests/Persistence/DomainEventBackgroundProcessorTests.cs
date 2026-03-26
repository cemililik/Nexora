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
        // Allow retry delay (100ms) + processing time
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
    public async Task ExecuteAsync_WithScopedPublisher_CreatesNewScopePerEvent()
    {
        var publisher = Substitute.For<IPublisher>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IPublisher)).Returns(publisher);

        var processor = new DomainEventBackgroundProcessor(
            _channel, scopeFactory, NullLogger<DomainEventBackgroundProcessor>.Instance);

        _channel.TryWrite(new TestDomainEvent("Scoped"));

        await processor.StartAsync(CancellationToken.None);
        await processor.StopAsync(CancellationToken.None);

        scopeFactory.Received().CreateScope();
        await publisher.Received(1).Publish(
            Arg.Is<TestDomainEvent>(e => e.Name == "Scoped"),
            Arg.Any<CancellationToken>());
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
}
