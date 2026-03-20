using Nexora.Modules.Notifications.Domain.ValueObjects;

namespace Nexora.Modules.Notifications.Tests.Domain;

public sealed class NotificationIdTests
{
    [Fact]
    public void New_CreatesUniqueIds()
    {
        // Arrange & Act
        var id1 = NotificationTemplateId.New();
        var id2 = NotificationTemplateId.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void From_CreatesFromGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = NotificationId.From(guid);

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Parse_CreatesFromString()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id = NotificationTemplateId.Parse(guid.ToString());

        // Assert
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ToString_ReturnsGuidString()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id = NotificationProviderId.From(guid);

        // Act
        var str = id.ToString();

        // Assert
        str.Should().Be(guid.ToString());
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var id1 = NotificationRecipientId.From(guid);
        var id2 = NotificationRecipientId.From(guid);

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        // Arrange & Act
        var id1 = NotificationScheduleId.New();
        var id2 = NotificationScheduleId.New();

        // Assert
        id1.Should().NotBe(id2);
    }
}
