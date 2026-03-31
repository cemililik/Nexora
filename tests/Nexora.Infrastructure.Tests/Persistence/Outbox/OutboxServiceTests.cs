using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Persistence.Outbox;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

public sealed class OutboxServiceTests : IDisposable
{
    private readonly OutboxDbContext _dbContext;
    private readonly OutboxService _sut;

    public OutboxServiceTests()
    {
        var options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new OutboxDbContext(options);
        _sut = new OutboxService(_dbContext, NullLogger<OutboxService>.Instance);
    }

    [Fact]
    public async Task EnqueueAsync_WithValidEvent_CreatesOutboxMessageWithCorrectFields()
    {
        // Arrange
        var @event = new TestIntegrationEvent { TenantId = "tenant-42", Data = "hello" };

        // Act
        await _sut.EnqueueAsync(@event);

        // Assert
        var messages = await _dbContext.OutboxMessages.ToListAsync();
        messages.Should().ContainSingle();

        var message = messages[0];
        message.EventType.Should().Contain(nameof(TestIntegrationEvent));
        message.TenantId.Should().Be("tenant-42");
        message.ProcessedAt.Should().BeNull();
        message.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_WithValidEvent_SerializesPayloadToValidJson()
    {
        // Arrange
        var @event = new TestIntegrationEvent { TenantId = "tenant-1", Data = "payload-data" };

        // Act
        await _sut.EnqueueAsync(@event);

        // Assert
        var message = await _dbContext.OutboxMessages.SingleAsync();
        var deserialized = JsonSerializer.Deserialize<TestIntegrationEvent>(message.EventPayload);
        deserialized.Should().NotBeNull();
        deserialized!.Data.Should().Be("payload-data");
        deserialized.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task EnqueueAsync_WithValidEvent_PersistsToDatabase()
    {
        // Arrange
        var @event = new TestIntegrationEvent { TenantId = "tenant-1" };

        // Act
        await _sut.EnqueueAsync(@event);

        // Assert — verify it survives a fresh query (persisted, not just tracked)
        var count = await _dbContext.OutboxMessages.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_WithMultipleEvents_PersistsAll()
    {
        // Arrange
        var event1 = new TestIntegrationEvent { TenantId = "tenant-1", Data = "first" };
        var event2 = new TestIntegrationEvent { TenantId = "tenant-2", Data = "second" };

        // Act
        await _sut.EnqueueAsync(event1);
        await _sut.EnqueueAsync(event2);

        // Assert
        var messages = await _dbContext.OutboxMessages.ToListAsync();
        messages.Should().HaveCount(2);
        messages.Select(m => m.TenantId).Should().Contain(["tenant-1", "tenant-2"]);
    }

    public void Dispose() => _dbContext.Dispose();

    private sealed record TestIntegrationEvent : IntegrationEventBase
    {
        public string Data { get; init; } = "test";
    }
}
