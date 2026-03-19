using Nexora.Modules.Contacts.Domain.ValueObjects;

namespace Nexora.Modules.Contacts.Tests.Domain;

public sealed class ContactIdTests
{
    [Fact]
    public void New_ShouldCreateUniqueIds()
    {
        // Arrange
        var id1 = ContactId.New();
        var id2 = ContactId.New();

        // Act & Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void From_ShouldWrapGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = ContactId.From(guid);

        // Act & Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Parse_ShouldParseString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = ContactId.Parse(guid.ToString());

        // Act & Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ToString_ShouldReturnGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = ContactId.From(guid);

        // Act & Assert
        id.ToString().Should().Be(guid.ToString());
    }

    [Fact]
    public void Equality_SameValue_ShouldBeEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = ContactId.From(guid);
        var id2 = ContactId.From(guid);

        // Act & Assert
        id1.Should().Be(id2);
    }
}
