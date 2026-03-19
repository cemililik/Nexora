using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class TagTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Major Donor", TagCategory.Donor, "#FF5733");

        // Act & Assert
        tag.Id.Value.Should().NotBeEmpty();
        tag.TenantId.Should().Be(_tenantId);
        tag.Name.Should().Be("Major Donor");
        tag.Category.Should().Be(TagCategory.Donor);
        tag.Color.Should().Be("#FF5733");
        tag.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "  Volunteer  ", TagCategory.Volunteer);

        // Act & Assert
        tag.Name.Should().Be("Volunteer");
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Old Name", TagCategory.Donor);

        // Act
        tag.Update("New Name", TagCategory.Parent, "#00FF00");

        // Assert
        tag.Name.Should().Be("New Name");
        tag.Category.Should().Be(TagCategory.Parent);
        tag.Color.Should().Be("#00FF00");
    }

    [Fact]
    public void Deactivate_ActiveItem_ShouldSetInactive()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Test", TagCategory.Staff);

        // Act
        tag.Deactivate();

        // Assert
        tag.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_InactiveTag_ShouldSetActive()
    {
        // Arrange
        var tag = Tag.Create(_tenantId, "Test", TagCategory.Staff);
        tag.Deactivate();

        // Act
        tag.Activate();

        // Assert
        tag.IsActive.Should().BeTrue();
    }
}
