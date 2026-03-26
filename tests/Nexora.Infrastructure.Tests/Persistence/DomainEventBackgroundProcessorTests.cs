using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence;

public sealed class DomainEventBackgroundProcessorTests
{
    private readonly DomainEventChannel _channel = new(
        Options.Create(new DomainEventChannelOptions { Capacity = 100 }));

    [Fact]
    public async Task ExecuteAsync_DispatchesEventFromChannel()
    {
        var publisher = Substitute.For<IPublisher>();
        var processor = CreateProcessor(publisher);

        _channel.TryWrite(new TestDomainEvent("E1"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = processor.StartAsync(cts.Token);

        // Wait for event to be processed
        await Task.Delay(200);
        await cts.CancelAsync();
        try { await task; } catch (OperationCanceledException) { }

        await publisher.Received(1).Publish(
            Arg.Is<TestDomainEvent>(e => e.Name == "E1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithTransientError_RetriesAndSucceeds()
    {
        var publisher = Substitute.For<IPublisher>();

        // Fail first call, succeed second
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var task = processor.StartAsync(cts.Token);
        await Task.Delay(500);
        await cts.CancelAsync();
        try { await task; } catch (OperationCanceledException) { }

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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = processor.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        try { await task; } catch (OperationCanceledException) { }

        callCount.Should().Be(1); // No retry for non-transient
    }

    [Fact]
    public async Task ExecuteAsync_ResolvesPublisherFromScope()
    {
        // Verify that the processor creates a scope and resolves IPublisher from it
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var task = processor.StartAsync(cts.Token);
        await Task.Delay(300);
        await cts.CancelAsync();
        try { await task; } catch (OperationCanceledException) { }

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
