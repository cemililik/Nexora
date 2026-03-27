using Nexora.SharedKernel.Domain.Base;

namespace Nexora.SharedKernel.Tests.Domain;

public class TestAuditableEntity : AuditableEntity<Guid>
{
    public static TestAuditableEntity Create()
    {
        return new TestAuditableEntity { Id = Guid.NewGuid() };
    }
}

public sealed class AuditableEntityTests
{
    [Fact]
    public void Create_ShouldHaveDefaultAuditValues()
    {
        var entity = TestAuditableEntity.Create();

        entity.CreatedAt.Should().Be(default);
        entity.CreatedBy.Should().BeNull();
        entity.UpdatedAt.Should().BeNull();
        entity.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void AuditableEntity_ShouldExtendEntity()
    {
        var entity = TestAuditableEntity.Create();

        entity.Should().BeAssignableTo<Entity<Guid>>();
        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void AuditableEntity_ShouldSupportDomainEvents()
    {
        var entity = TestAuditableEntity.Create();

        entity.Should().BeAssignableTo<IHasDomainEvents>();
        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkAsDeleted_ShouldSetDeletedFields()
    {
        var entity = TestAuditableEntity.Create();
        var now = DateTimeOffset.UtcNow;

        entity.MarkAsDeleted(now, "admin");

        entity.IsDeleted.Should().BeTrue();
        entity.DeletedAt.Should().Be(now);
        entity.DeletedBy.Should().Be("admin");
    }

    [Fact]
    public void MarkAsDeleted_WithDefaultTimestamp_ShouldThrow()
    {
        var entity = TestAuditableEntity.Create();

        var act = () => entity.MarkAsDeleted(default, "admin");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("at");
    }

    [Fact]
    public void UndoDelete_ShouldClearDeletedFields()
    {
        var entity = TestAuditableEntity.Create();
        entity.MarkAsDeleted(DateTimeOffset.UtcNow, "admin");

        entity.UndoDelete();

        entity.IsDeleted.Should().BeFalse();
        entity.DeletedAt.Should().BeNull();
        entity.DeletedBy.Should().BeNull();
    }
}
