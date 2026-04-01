namespace Nexora.Infrastructure.Persistence.Inbox;

/// <summary>
/// Tracks processed integration events for idempotent consumption.
/// Stored in each module's tenant schema to ensure deduplication per module.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>The unique identifier of the integration event.</summary>
    public Guid EventId { get; private set; }

    /// <summary>The fully qualified type name of the integration event.</summary>
    public string EventType { get; private set; } = default!;

    /// <summary>UTC timestamp when the event was processed.</summary>
    public DateTimeOffset ProcessedAt { get; private set; } = DateTimeOffset.UtcNow;

    private InboxMessage() { }

    /// <summary>Creates a new inbox record for the given event.</summary>
    public static InboxMessage Create(Guid eventId, string eventType) =>
        new() { EventId = eventId, EventType = eventType };
}
