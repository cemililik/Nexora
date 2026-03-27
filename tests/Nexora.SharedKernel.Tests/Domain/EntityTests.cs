using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.SharedKernel.Tests.Domain;

public class TestEntity : Entity<Guid>
{
    public string Name { get; set; } = default!;

    public static TestEntity Create(string name)
    {
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = name };
        return entity;
    }

    public void RaiseDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);
}

public sealed record TestDomainEvent(string Data) : DomainEventBase;

public sealed class EntityTests
{
    [Fact]
    public void Create_ShouldSetId()
    {
        var entity = TestEntity.Create("test");

        entity.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_ShouldAddToCollection()
    {
        var entity = TestEntity.Create("test");
        var domainEvent = new TestDomainEvent("data");

        entity.RaiseDomainEvent(domainEvent);

        entity.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAll()
    {
        var entity = TestEntity.Create("test");
        entity.RaiseDomainEvent(new TestDomainEvent("1"));
        entity.RaiseDomainEvent(new TestDomainEvent("2"));

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Equals_SameId_ShouldBeTrue()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity { Name = "a" };
        var entity2 = new TestEntity { Name = "b" };

        // Use reflection to set same Id
        typeof(Entity<Guid>).GetProperty("Id")!.SetValue(entity1, id);
        typeof(Entity<Guid>).GetProperty("Id")!.SetValue(entity2, id);

        entity1.Equals(entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentId_ShouldBeFalse()
    {
        var entity1 = TestEntity.Create("a");
        var entity2 = TestEntity.Create("b");

        entity1.Equals(entity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        var entity = TestEntity.Create("test");

        entity.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_SameReference_ShouldBeTrue()
    {
        var entity = TestEntity.Create("test");

        entity.Equals(entity).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentType_ShouldBeFalse()
    {
        var entity = TestEntity.Create("test");

        entity.Equals("not an entity").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity { Name = "a" };
        var entity2 = new TestEntity { Name = "b" };

        typeof(Entity<Guid>).GetProperty("Id")!.SetValue(entity1, id);
        typeof(Entity<Guid>).GetProperty("Id")!.SetValue(entity2, id);

        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void IHasDomainEvents_ShouldBeImplemented()
    {
        var entity = TestEntity.Create("test");

        entity.Should().BeAssignableTo<IHasDomainEvents>();
    }
}
