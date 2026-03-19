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
}
