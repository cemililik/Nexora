using System.Text.Json;

namespace Nexora.Modules.Notifications.Infrastructure.Webhooks;

/// <summary>
/// Parses provider-specific webhook payloads into a normalized delivery event.
/// </summary>
public static class WebhookPayloadParser
{
    /// <summary>
    /// Parses a SendGrid event webhook payload.
    /// SendGrid sends an array of events: [{ "event": "delivered", "sg_message_id": "...", ... }]
    /// </summary>
    public static IReadOnlyList<WebhookEvent> ParseSendGrid(string payload)
    {
        var events = new List<WebhookEvent>();

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return events;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var eventType = element.TryGetProperty("event", out var e) ? e.GetString() : null;
            var messageId = element.TryGetProperty("sg_message_id", out var m) ? m.GetString() : null;
            var reason = element.TryGetProperty("reason", out var r) ? r.GetString() : null;

            if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(messageId))
                continue;

            var status = MapSendGridEvent(eventType);
            if (status is null)
                continue;

            events.Add(new WebhookEvent(messageId, status, reason));
        }

        return events;
    }

    /// <summary>
    /// Parses a Twilio status callback payload (form-encoded converted to dictionary).
    /// </summary>
    public static WebhookEvent? ParseTwilio(Dictionary<string, string> formData)
    {
        if (!formData.TryGetValue("MessageSid", out var messageSid) || string.IsNullOrWhiteSpace(messageSid))
            return null;

        if (!formData.TryGetValue("MessageStatus", out var messageStatus) || string.IsNullOrWhiteSpace(messageStatus))
            return null;

        var status = MapTwilioStatus(messageStatus);
        if (status is null)
            return null;

        formData.TryGetValue("ErrorMessage", out var errorMessage);
        return new WebhookEvent(messageSid, status, errorMessage);
    }

    private static string? MapSendGridEvent(string eventType) => eventType.ToLowerInvariant() switch
    {
        "delivered" => "delivered",
        "open" => "opened",
        "bounce" => "bounced",
        "dropped" => "failed",
        "deferred" => null, // Transient, not a final state
        "processed" => null,
        _ => null
    };

    private static string? MapTwilioStatus(string status) => status.ToLowerInvariant() switch
    {
        "delivered" => "delivered",
        "undelivered" => "failed",
        "failed" => "failed",
        _ => null
    };
}

/// <summary>Normalized webhook event from any provider.</summary>
public sealed record WebhookEvent(
    string ProviderMessageId,
    string Status,
    string? FailureReason = null);
