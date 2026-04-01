namespace Nexora.Infrastructure.Tests.Persistence.Outbox;

using Nexora.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Create_WithValidParameters_SetsFieldsCorrectly()
    {
        // Arrange
        var eventType = "Nexora.Tests.SomeEvent";
        var payload = """{"Data":"test"}""";
        var tenantId = "tenant-1";

        // Act
        var message = OutboxMessage.Create(eventType, payload, tenantId);

        // Assert
        message.Id.Should().NotBeEmpty();
        message.EventType.Should().Be(eventType);
        message.EventPayload.Should().Be(payload);
        message.TenantId.Should().Be(tenantId);
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        message.ProcessedAt.Should().BeNull();
        message.Error.Should().BeNull();
        message.RetryCount.Should().Be(0);
    }

    [Fact]
    public void MarkProcessed_WhenCalled_SetsProcessedAt()
    {
        // Arrange
        var message = OutboxMessage.Create("SomeType", "{}", "tenant-1");

        // Act
        message.MarkProcessed();

        // Assert
        message.ProcessedAt.Should().NotBeNull();
        message.ProcessedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void RecordFailure_WhenCalled_IncrementsRetryCountAndSetsError()
    {
        // Arrange
        var message = OutboxMessage.Create("SomeType", "{}", "tenant-1");
        var error = "Something went wrong";

        // Act
        message.RecordFailure(error);

        // Assert
        message.RetryCount.Should().Be(1);
        message.Error.Should().Be(error);
    }

    [Fact]
    public void RecordFailure_CalledMultipleTimes_IncrementsRetryCountEachTime()
    {
        // Arrange
        var message = OutboxMessage.Create("SomeType", "{}", "tenant-1");

        // Act
        message.RecordFailure("Error 1");
        message.RecordFailure("Error 2");
        message.RecordFailure("Error 3");

        // Assert
        message.RetryCount.Should().Be(3);
        message.Error.Should().Be("Error 3");
    }
}
