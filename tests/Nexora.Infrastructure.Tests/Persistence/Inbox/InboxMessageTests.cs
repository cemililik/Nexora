namespace Nexora.Infrastructure.Tests.Persistence.Inbox;

using Nexora.Infrastructure.Persistence.Inbox;

public sealed class InboxMessageTests
{
    [Fact]
    public void Create_WithValidParameters_SetsFieldsCorrectly()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var eventType = "Nexora.Tests.SomeIntegrationEvent";

        // Act
        var message = InboxMessage.Create(eventId, eventType);

        // Assert
        message.EventId.Should().Be(eventId);
        message.EventType.Should().Be(eventType);
        message.ProcessedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithDifferentEventIds_ProducesDifferentInstances()
    {
        // Arrange
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        // Act
        var message1 = InboxMessage.Create(eventId1, "TypeA");
        var message2 = InboxMessage.Create(eventId2, "TypeB");

        // Assert
        message1.EventId.Should().NotBe(message2.EventId);
        message1.EventType.Should().NotBe(message2.EventType);
    }
}
