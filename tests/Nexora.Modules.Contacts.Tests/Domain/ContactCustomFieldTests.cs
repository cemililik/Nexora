using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactCustomFieldTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var contactId = ContactId.New();
        var defId = CustomFieldDefinitionId.New();

        // Act
        var field = ContactCustomField.Create(contactId, defId, "2000-01-15");

        // Assert
        field.Id.Value.Should().NotBeEmpty();
        field.ContactId.Should().Be(contactId);
        field.FieldDefinitionId.Should().Be(defId);
        field.Value.Should().Be("2000-01-15");
    }

    [Fact]
    public void UpdateValue_ShouldChangeValue()
    {
        // Arrange
        var field = ContactCustomField.Create(ContactId.New(), CustomFieldDefinitionId.New(), "old");

        // Act
        field.UpdateValue("new");

        // Assert
        field.Value.Should().Be("new");
    }

    [Fact]
    public void Create_NullValue_ShouldBeNull()
    {
        // Arrange
        var field = ContactCustomField.Create(ContactId.New(), CustomFieldDefinitionId.New(), null);

        // Act & Assert
        field.Value.Should().BeNull();
    }
}
