using Nexora.Modules.Contacts.Domain.Entities;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class CustomFieldDefinitionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var field = CustomFieldDefinition.Create(
            _tenantId, "Birthday", "date", isRequired: false, displayOrder: 1);

        // Act & Assert
        field.Id.Value.Should().NotBeEmpty();
        field.TenantId.Should().Be(_tenantId);
        field.FieldName.Should().Be("Birthday");
        field.FieldType.Should().Be("date");
        field.IsRequired.Should().BeFalse();
        field.DisplayOrder.Should().Be(1);
        field.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldNormalizeFieldType()
    {
        // Arrange
        var field = CustomFieldDefinition.Create(_tenantId, "Test", "SELECT");

        // Act & Assert
        field.FieldType.Should().Be("select");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        // Arrange
        var field = CustomFieldDefinition.Create(_tenantId, "Old", "text");

        // Act
        field.Update("New Name", "[\"a\",\"b\"]", true, 5);

        // Assert
        field.FieldName.Should().Be("New Name");
        field.Options.Should().Be("[\"a\",\"b\"]");
        field.IsRequired.Should().BeTrue();
        field.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void Deactivate_ActiveItem_ShouldSetInactive()
    {
        // Arrange
        var field = CustomFieldDefinition.Create(_tenantId, "Test", "text");

        // Act
        field.Deactivate();

        // Assert
        field.IsActive.Should().BeFalse();
    }
}
