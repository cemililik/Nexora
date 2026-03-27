using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Nexora.Infrastructure.Persistence;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Events;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Tests.Persistence;

public sealed record TestDomainEvent(string Name) : DomainEventBase;

public sealed class TestEntity : Entity<Guid>, IHasDomainEvents
{
    private TestEntity() { }

    public static TestEntity Create()
    {
        var entity = new TestEntity();
        typeof(Entity<Guid>).GetProperty("Id")!.SetValue(entity, Guid.NewGuid());
        return entity;
    }

    public void RaiseEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
    : DbContext(options)
{
    public DbSet<TestEntity> TestEntities => Set<TestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Ignore(x => x.DomainEvents);
        });
    }
}

public sealed class DomainEventDispatcherTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly DomainEventChannel _channel;
    private readonly DomainEventDispatcher _dispatcher;

    public DomainEventDispatcherTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _dbContext = new TestDbContext(options);

        _publisher = Substitute.For<IPublisher>();
        _channel = new DomainEventChannel(Options.Create(new DomainEventChannelOptions()),
            NullLogger<DomainEventChannel>.Instance);
        _dispatcher = new DomainEventDispatcher(
            _publisher, _channel, NullLogger<DomainEventDispatcher>.Instance);
    }

    [Fact]
    public async Task DispatchEventsAsync_WithPendingEvents_PublishesAllAndClearsEntities()
    {
        var entity = TestEntity.Create();
        entity.RaiseEvent(new TestDomainEvent("A"));
        entity.RaiseEvent(new TestDomainEvent("B"));
        _dbContext.TestEntities.Add(entity);
        await _dbContext.SaveChangesAsync();

        await _dispatcher.DispatchEventsAsync(_dbContext, CancellationToken.None);

        await _publisher.Received(2).Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchEventsAsync_WithNoEvents_DoesNotPublish()
    {
        var entity = TestEntity.Create();
        _dbContext.TestEntities.Add(entity);
        await _dbContext.SaveChangesAsync();

        await _dispatcher.DispatchEventsAsync(_dbContext, CancellationToken.None);

        await _publisher.DidNotReceive().Publish(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchEvents_WhenEntityRaisesEvent_EnqueuesEventToChannel()
    {
        var entity = TestEntity.Create();
        entity.RaiseEvent(new TestDomainEvent("Queued"));
        _dbContext.TestEntities.Add(entity);
        await _dbContext.SaveChangesAsync();

        _dispatcher.DispatchEvents(_dbContext);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var events = new List<IDomainEvent>();
        await foreach (var e in _channel.ReadAllAsync(cts.Token))
        {
            events.Add(e);
            break;
        }
        events.Should().ContainSingle()
            .Which.Should().BeOfType<TestDomainEvent>()
            .Which.Name.Should().Be("Queued");
    }

    [Fact]
    public async Task DispatchEvents_WithPendingEvents_ClearsEntitiesAfterQueuing()
    {
        var entity = TestEntity.Create();
        entity.RaiseEvent(new TestDomainEvent("X"));
        _dbContext.TestEntities.Add(entity);
        await _dbContext.SaveChangesAsync();

        _dispatcher.DispatchEvents(_dbContext);

        entity.DomainEvents.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
