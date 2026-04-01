using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Testcontainers.PostgreSql;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

public sealed class OutboxProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private OutboxDbContext _dbContext = null!;
    private IEventBus _eventBus = null!;
    private OutboxProcessor _processor = null!;
    private OutboxOptions _options = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new OutboxDbContext(dbOptions);
        await _dbContext.Database.EnsureCreatedAsync();

        _eventBus = Substitute.For<IEventBus>();
        _options = new OutboxOptions
        {
            PollingIntervalSeconds = 1,
            BatchSize = 100,
            MaxRetryCount = 3,
            ConnectionString = _postgres.GetConnectionString()
        };

        var tenantProvider = Substitute.For<IActiveTenantProvider>();
        tenantProvider.GetActiveTenantsAsync(Arg.Any<CancellationToken>())
            .Returns([new ActiveTenantInfo("tenant-1", "public")]);

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        services.AddSingleton(_eventBus);
        services.AddSingleton(tenantProvider);

        var provider = services.BuildServiceProvider();

        _processor = new OutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<OutboxProcessor>.Instance);
    }

    public async Task DisposeAsync()
    {
        _dbContext.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProcessPendingMessages_WithPendingMessage_PublishesAndMarksProcessed()
    {
        // Arrange
        var @event = new TestProcessorEvent { TenantId = "tenant-1", Data = "hello" };
        var eventType = typeof(TestProcessorEvent).AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event);
        var message = OutboxMessage.Create(eventType, payload, "tenant-1");
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act — start the processor; poll until the message is processed or timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _processor.StartAsync(cts.Token);
        await WaitUntilProcessedAsync(message.Id, TimeSpan.FromSeconds(5));
        await _processor.StopAsync(CancellationToken.None);

        // Assert
        await _eventBus.Received(1).PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());

        var processed = await _dbContext.OutboxMessages.SingleAsync();
        processed.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
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

        // Act — start and wait for 2 polling cycles so the processor has a chance to skip/process
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _processor.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2)); // 2 × PollingIntervalSeconds
        await _processor.StopAsync(CancellationToken.None);

        // Assert
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Integration")]
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

        // Act — let processor attempt once
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _processor.StartAsync(cts.Token);
        await WaitUntilRetryIncrementedAsync(message.Id, TimeSpan.FromSeconds(4));
        await _processor.StopAsync(CancellationToken.None);

        // Assert
        _dbContext.ChangeTracker.Clear();
        var failed = await _dbContext.OutboxMessages.SingleAsync();
        failed.RetryCount.Should().BeGreaterThanOrEqualTo(1);
        failed.Error.Should().Contain("Broker unavailable");
        failed.ProcessedAt.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
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

        // Act — start and wait for 2 polling cycles so the processor has a chance to skip
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _processor.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(2)); // 2 × PollingIntervalSeconds
        await _processor.StopAsync(CancellationToken.None);

        // Assert — should not attempt publish since RetryCount >= MaxRetryCount
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<TestProcessorEvent>(), Arg.Any<CancellationToken>());

        _dbContext.ChangeTracker.Clear();
        var skipped = await _dbContext.OutboxMessages.SingleAsync();
        skipped.ProcessedAt.Should().BeNull();
        skipped.RetryCount.Should().Be(_options.MaxRetryCount);
    }

    /// <summary>Polls until the given outbox message has a non-null ProcessedAt, or throws if the timeout elapses.</summary>
    private async Task WaitUntilProcessedAsync(Guid messageId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            _dbContext.ChangeTracker.Clear();
            var msg = await _dbContext.OutboxMessages.FindAsync(messageId);
            if (msg?.ProcessedAt is not null) return;
            await Task.Delay(100);
        }
        throw new TimeoutException(
            $"Outbox message {messageId} was not marked as processed within {timeout.TotalSeconds}s.");
    }

    /// <summary>Polls until the given outbox message RetryCount is > 0, or throws if the timeout elapses.</summary>
    private async Task WaitUntilRetryIncrementedAsync(Guid messageId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            _dbContext.ChangeTracker.Clear();
            var msg = await _dbContext.OutboxMessages.FindAsync(messageId);
            if (msg?.RetryCount > 0) return;
            await Task.Delay(100);
        }
        throw new TimeoutException(
            $"Outbox message {messageId} RetryCount was not incremented within {timeout.TotalSeconds}s.");
    }
}
