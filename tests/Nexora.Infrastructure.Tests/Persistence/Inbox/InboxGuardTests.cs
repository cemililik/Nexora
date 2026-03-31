using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence.Inbox;

namespace Nexora.Infrastructure.Tests.Persistence.Inbox;

/// <summary>
/// Test DbContext that includes InboxMessage for InboxGuard testing.
/// </summary>
public sealed class InboxTestDbContext(DbContextOptions<InboxTestDbContext> options)
    : DbContext(options)
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
    }
}

public sealed class InboxGuardTests : IDisposable
{
    private readonly InboxTestDbContext _dbContext;
    private readonly InboxGuard<InboxTestDbContext> _sut;

    public InboxGuardTests()
    {
        var options = new DbContextOptionsBuilder<InboxTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new InboxTestDbContext(options);
        _sut = new InboxGuard<InboxTestDbContext>(_dbContext);
    }

    [Fact]
    public async Task IsAlreadyProcessedAsync_WithNewEventId_ReturnsFalse()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        var result = await _sut.IsAlreadyProcessedAsync(eventId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAlreadyProcessedAsync_AfterMarkAsProcessedAndSave_ReturnsTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _sut.MarkAsProcessed(eventId, "TestEventType");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _sut.IsAlreadyProcessedAsync(eventId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MarkAsProcessed_WhenCalled_AddsToChangeTracker()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        _sut.MarkAsProcessed(eventId, "TestEventType");

        // Assert
        _dbContext.ChangeTracker.Entries<InboxMessage>()
            .Should().ContainSingle()
            .Which.State.Should().Be(EntityState.Added);
    }

    [Fact]
    public void MarkAsProcessed_WithSameEventIdTwice_ThrowsOnTrack()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _sut.MarkAsProcessed(eventId, "TestEventType");

        // Act — mark same event again (InMemory throws on Add, not SaveChanges)
        var act = () => _sut.MarkAsProcessed(eventId, "TestEventType");

        // Assert — EF Core rejects duplicate tracked entity with same PK
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task IsAlreadyProcessedAsync_BeforeSaveChanges_ReturnsFalse()
    {
        // Arrange — mark but do NOT save
        var eventId = Guid.NewGuid();
        _sut.MarkAsProcessed(eventId, "TestEventType");

        // Act — AnyAsync queries the store, not the change tracker
        var result = await _sut.IsAlreadyProcessedAsync(eventId);

        // Assert — InMemory provider does not see unsaved tracked entities via AnyAsync
        result.Should().BeFalse();
    }

    public void Dispose() => _dbContext.Dispose();
}
