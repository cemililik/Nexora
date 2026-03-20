using Nexora.Modules.Notifications.Infrastructure.Webhooks;

namespace Nexora.Modules.Notifications.Tests.Infrastructure;

public sealed class WebhookPayloadParserTests
{
    [Fact]
    public void ParseSendGrid_ValidPayload_ShouldParseEvents()
    {
        // Arrange
        var payload = """
        [
            {"event": "delivered", "sg_message_id": "msg_001"},
            {"event": "open", "sg_message_id": "msg_002"},
            {"event": "bounce", "sg_message_id": "msg_003", "reason": "Mailbox full"}
        ]
        """;

        // Act
        var events = WebhookPayloadParser.ParseSendGrid(payload);

        // Assert
        events.Should().HaveCount(3);
        events[0].Status.Should().Be("delivered");
        events[0].ProviderMessageId.Should().Be("msg_001");
        events[1].Status.Should().Be("opened");
        events[2].Status.Should().Be("bounced");
        events[2].FailureReason.Should().Be("Mailbox full");
    }

    [Fact]
    public void ParseSendGrid_EmptyArray_ShouldReturnEmpty()
    {
        // Arrange & Act
        var events = WebhookPayloadParser.ParseSendGrid("[]");

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public void ParseSendGrid_InvalidJson_ShouldReturnEmpty()
    {
        // Arrange & Act
        var events = WebhookPayloadParser.ParseSendGrid("{}");

        // Assert
        events.Should().BeEmpty();
    }

    [Fact]
    public void ParseSendGrid_SkipsUnknownEvents_ShouldFilterCorrectly()
    {
        // Arrange
        var payload = """
        [
            {"event": "processed", "sg_message_id": "msg_001"},
            {"event": "deferred", "sg_message_id": "msg_002"},
            {"event": "delivered", "sg_message_id": "msg_003"}
        ]
        """;

        // Act
        var events = WebhookPayloadParser.ParseSendGrid(payload);

        // Assert
        events.Should().HaveCount(1);
        events[0].Status.Should().Be("delivered");
    }

    [Fact]
    public void ParseSendGrid_MissingFields_ShouldSkipInvalid()
    {
        // Arrange
        var payload = """
        [
            {"event": "delivered"},
            {"sg_message_id": "msg_001"},
            {"event": "delivered", "sg_message_id": "msg_002"}
        ]
        """;

        // Act
        var events = WebhookPayloadParser.ParseSendGrid(payload);

        // Assert
        events.Should().HaveCount(1);
        events[0].ProviderMessageId.Should().Be("msg_002");
    }

    [Fact]
    public void ParseTwilio_ValidPayload_ShouldParseEvent()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            ["MessageSid"] = "SM_001",
            ["MessageStatus"] = "delivered"
        };

        // Act
        var result = WebhookPayloadParser.ParseTwilio(formData);

        // Assert
        result.Should().NotBeNull();
        result!.ProviderMessageId.Should().Be("SM_001");
        result.Status.Should().Be("delivered");
    }

    [Fact]
    public void ParseTwilio_FailedStatus_ShouldIncludeErrorMessage()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            ["MessageSid"] = "SM_002",
            ["MessageStatus"] = "failed",
            ["ErrorMessage"] = "Invalid phone number"
        };

        // Act
        var result = WebhookPayloadParser.ParseTwilio(formData);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("failed");
        result.FailureReason.Should().Be("Invalid phone number");
    }

    [Fact]
    public void ParseTwilio_MissingSid_ShouldReturnNull()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            ["MessageStatus"] = "delivered"
        };

        // Act
        var result = WebhookPayloadParser.ParseTwilio(formData);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ParseTwilio_UnknownStatus_ShouldReturnNull()
    {
        // Arrange
        var formData = new Dictionary<string, string>
        {
            ["MessageSid"] = "SM_003",
            ["MessageStatus"] = "queued"
        };

        // Act
        var result = WebhookPayloadParser.ParseTwilio(formData);

        // Assert
        result.Should().BeNull();
    }
}
