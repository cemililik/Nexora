using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Persistence.Outbox;

namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

public sealed class OutboxServiceTests : IDisposable
{
    private readonly OutboxTestDbContext _dbContext;
    private readonly OutboxService<OutboxTestDbContext> _sut;

    public OutboxServiceTests()
    {
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new OutboxTestDbContext(options);
        _sut = new OutboxService<OutboxTestDbContext>(_dbContext, NullLogger<OutboxService<OutboxTestDbContext>>.Instance);
    }

    [Fact]
    public async Task EnqueueAsync_WithValidEvent_CreatesOutboxMessageWithCorrectFields()
    {
        var @event = new TestIntegrationEvent { TenantId = "tenant-42", Data = "hello" };

        await _sut.EnqueueAsync(@event);
        await _dbContext.SaveChangesAsync(); // Caller is responsible for saving

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
        var @event = new TestIntegrationEvent { TenantId = "tenant-1", Data = "payload-data" };

        await _sut.EnqueueAsync(@event);
        await _dbContext.SaveChangesAsync();

        var message = await _dbContext.OutboxMessages.SingleAsync();
        var deserialized = JsonSerializer.Deserialize<TestIntegrationEvent>(message.EventPayload);
        deserialized.Should().NotBeNull();
        deserialized!.Data.Should().Be("payload-data");
        deserialized.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task EnqueueAsync_WithoutSaveChanges_OnlyStagesInChangeTracker()
    {
        var @event = new TestIntegrationEvent { TenantId = "tenant-1" };

        await _sut.EnqueueAsync(@event);
        // Deliberately NOT calling SaveChangesAsync

        // Verify the message is staged in the change tracker but not yet persisted
        var tracked = _dbContext.ChangeTracker.Entries<OutboxMessage>().Count();
        tracked.Should().Be(1);

        var entry = _dbContext.ChangeTracker.Entries<OutboxMessage>().Single();
        entry.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public async Task EnqueueAsync_WithMultipleEvents_PersistsAll()
    {
        var event1 = new TestIntegrationEvent { TenantId = "tenant-1", Data = "first" };
        var event2 = new TestIntegrationEvent { TenantId = "tenant-2", Data = "second" };

        await _sut.EnqueueAsync(event1);
        await _sut.EnqueueAsync(event2);
        await _dbContext.SaveChangesAsync();

        var messages = await _dbContext.OutboxMessages.ToListAsync();
        messages.Should().HaveCount(2);
        messages.Select(m => m.TenantId).Should().Contain(["tenant-1", "tenant-2"]);
    }

    public void Dispose() => _dbContext.Dispose();
}
