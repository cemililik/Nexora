using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nexora.Infrastructure.Audit;
using Nexora.SharedKernel.Abstractions.Audit;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Infrastructure.Tests.Audit;

public sealed class AuditChangeTrackerInterceptorTests : IDisposable
{
    private readonly AuditStateCapture _capture = new();
    private readonly TestDbContext _db;

    public AuditChangeTrackerInterceptorTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditChangeTrackerInterceptor(
                _capture,
                NullLogger<AuditChangeTrackerInterceptor>.Instance))
            .Options;
        _db = new TestDbContext(options);
    }

    private static (object? From, object? To) FromTo(object? diff)
    {
        var type = diff!.GetType();
        return (type.GetProperty("From")!.GetValue(diff), type.GetProperty("To")!.GetValue(diff));
    }

    [Fact]
    public async Task Added_entity_is_captured_with_after_snapshot_and_no_before()
    {
        var entity = TestEntity.Create("Alice", 10);
        _db.Entities.Add(entity);

        await _db.SaveChangesAsync();

        _capture.Changes.Should().HaveCount(1);
        var change = _capture.Changes[0];
        change.Kind.Should().Be(EntityChangeKind.Added);
        change.EntityType.Should().Be(nameof(TestEntity));
        change.After["Name"].Should().Be("Alice");
        change.After["Score"].Should().Be(10);
        change.Before.Should().BeEmpty();
        FromTo(change.Delta["Name"]).Should().Be(((object?)null, (object?)"Alice"));
    }

    [Fact]
    public async Task Modified_entity_captures_before_after_and_delta_only_for_changed_fields()
    {
        var entity = TestEntity.Create("Alice", 10);
        _db.Entities.Add(entity);
        await _db.SaveChangesAsync();
        _capture.Clear();

        entity.Rename("Alicia");
        await _db.SaveChangesAsync();

        var change = _capture.Changes.Single();
        change.Kind.Should().Be(EntityChangeKind.Modified);
        change.Before["Name"].Should().Be("Alice");
        change.After["Name"].Should().Be("Alicia");
        change.Delta.Should().ContainKey("Name");
        change.Delta.Should().NotContainKey("Score");
        FromTo(change.Delta["Name"]).Should().Be(((object?)"Alice", (object?)"Alicia"));
    }

    [Fact]
    public async Task Soft_delete_is_captured_as_deleted_kind()
    {
        var entity = TestEntity.Create("Alice", 10);
        _db.Entities.Add(entity);
        await _db.SaveChangesAsync();
        _capture.Clear();

        _db.Entities.Remove(entity);
        await _db.SaveChangesAsync();

        var change = _capture.Changes.Single();
        change.Kind.Should().Be(EntityChangeKind.Deleted);
        change.Before["Name"].Should().Be("Alice");
    }

    [Fact]
    public async Task Noisy_audit_fields_are_excluded_from_snapshots()
    {
        var entity = TestEntity.Create("Alice", 10);
        _db.Entities.Add(entity);
        await _db.SaveChangesAsync();

        var change = _capture.Changes.Single();
        change.After.Should().NotContainKey("CreatedAt");
        change.After.Should().NotContainKey("CreatedBy");
        change.After.Should().NotContainKey("UpdatedAt");
        change.After.Should().NotContainKey("UpdatedBy");
    }

    public void Dispose() => _db.Dispose();

    // --- Test doubles ---

    private sealed class TestEntity : AuditableEntity<Guid>
    {
        public string Name { get; private set; } = default!;
        public int Score { get; private set; }

        private TestEntity() { }

        public static TestEntity Create(string name, int score) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Score = score
        };

        public void Rename(string name) => Name = name;
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Name).IsRequired();
            });
        }
    }
}
