using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly OutboxDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly OutboxOptions _options;
    private readonly OutboxProcessor _processor;

    public OutboxProcessorTests()
    {
        var dbOptions = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new OutboxDbContext(dbOptions);
        _eventBus = Substitute.For<IEventBus>();
        _options = new OutboxOptions
        {
            PollingIntervalSeconds = 1,
            BatchSize = 100,
            MaxRetryCount = 3
        };

        // Build a real IServiceScopeFactory that resolves our test instances
        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddSingleton<OutboxDbContext>(_ => _dbContext);
        services.AddSingleton(_eventBus);
        var provider = services.BuildServiceProvider();

        _processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<OutboxProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessPendingMessages_WithPendingMessage_PublishesAndMarksProcessed()
    {
        // Arrange
        var @event = new TestProcessorEvent { TenantId = "tenant-1", Data = "hello" };
        var eventType = typeof(TestProcessorEvent).AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload, "tenant-1");
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act — execute one cycle by starting and cancelling quickly
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await _processor.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await _processor.StopAsync(CancellationToken.None); }

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());

        var processed = await _dbContext.OutboxMessages.SingleAsync();
        processed.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingMessages_AlreadyProcessed_DoesNotPublishAgain()
    {
        // Arrange — create a message that is already processed
        var @event = new TestProcessorEvent { TenantId = "tenant-1" };
        var eventType = typeof(TestProcessorEvent).AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload, "tenant-1");
        message.MarkProcessed();
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await _processor.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await _processor.StopAsync(CancellationToken.None); }

        // Assert
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingMessages_WhenPublishFails_RecordsErrorAndIncrementsRetryCount()
    {
        // Arrange
        var @event = new TestProcessorEvent { TenantId = "tenant-1" };
        var eventType = typeof(TestProcessorEvent).AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload, "tenant-1");
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        _eventBus.PublishAsync(Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Broker unavailable"));

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await _processor.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await _processor.StopAsync(CancellationToken.None); }

        // Assert
        var failed = await _dbContext.OutboxMessages.SingleAsync();
        failed.RetryCount.Should().BeGreaterThanOrEqualTo(1);
        failed.Error.Should().Contain("Broker unavailable");
        failed.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPendingMessages_MaxRetryExceeded_SkipsMessage()
    {
        // Arrange — create a message already at max retry
        var @event = new TestProcessorEvent { TenantId = "tenant-1" };
        var eventType = typeof(TestProcessorEvent).AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload, "tenant-1");
        for (var i = 0; i < _options.MaxRetryCount; i++)
            message.RecordFailure($"Failure {i + 1}");
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await _processor.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await _processor.StopAsync(CancellationToken.None); }

        // Assert — should not attempt publish since RetryCount >= MaxRetryCount
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());

        var skipped = await _dbContext.OutboxMessages.SingleAsync();
        skipped.ProcessedAt.Should().BeNull();
        skipped.RetryCount.Should().Be(_options.MaxRetryCount);
    }

    public void Dispose() => _dbContext.Dispose();
}

/// <summary>
/// Test integration event used by OutboxProcessor tests.
/// Must be public so Type.GetType can resolve it and JSON deserialization works.
/// </summary>
public sealed record TestProcessorEvent : IntegrationEventBase
{
    public string Data { get; init; } = "test";
}
