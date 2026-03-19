using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactRelationshipTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var contactId = ContactId.New();
        var relatedId = ContactId.New();

        // Act
        var rel = ContactRelationship.Create(contactId, relatedId, RelationshipType.ParentOf);

        // Assert
        rel.Id.Value.Should().NotBeEmpty();
        rel.ContactId.Should().Be(contactId);
        rel.RelatedContactId.Should().Be(relatedId);
        rel.Type.Should().Be(RelationshipType.ParentOf);
        rel.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_SpouseOf_ShouldSetType()
    {
        // Arrange
        var rel = ContactRelationship.Create(ContactId.New(), ContactId.New(), RelationshipType.SpouseOf);

        // Act & Assert
        rel.Type.Should().Be(RelationshipType.SpouseOf);
    }

    [Fact]
    public void Create_EmployeeOf_ShouldSetType()
    {
        // Arrange
        var rel = ContactRelationship.Create(ContactId.New(), ContactId.New(), RelationshipType.EmployeeOf);

        // Act & Assert
        rel.Type.Should().Be(RelationshipType.EmployeeOf);
    }

    [Fact]
    public void Create_ShouldHaveDistinctIds()
    {
        // Arrange
        var rel1 = ContactRelationship.Create(ContactId.New(), ContactId.New(), RelationshipType.ParentOf);
        var rel2 = ContactRelationship.Create(ContactId.New(), ContactId.New(), RelationshipType.ChildOf);

        // Act & Assert
        rel1.Id.Should().NotBe(rel2.Id);
    }
}
