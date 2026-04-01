namespace Nexora.Infrastructure.Persistence.Outbox;

/// <summary>
/// Represents a pending integration event stored in the outbox table.
/// Ensures reliable event publishing by persisting events to the database
/// before they are dispatched to the message broker.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string EventType { get; private set; } = default!;
    public string EventPayload { get; private set; } = default!;
    public string TenantId { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    /// <summary>Creates a new outbox message from a serialized integration event.</summary>
    public static OutboxMessage Create(string eventType, string payload, string tenantId) =>
        new() { EventType = eventType, EventPayload = payload, TenantId = tenantId };

    /// <summary>Marks this message as successfully processed.</summary>
    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;

    /// <summary>Records a processing failure, incrementing the retry count.</summary>
    public void RecordFailure(string error)
    {
        Error = error;
        RetryCount++;
    }

    /// <summary>Resets retry count and error so the processor picks this message up again.</summary>
    public void ResetForRetry()
    {
        RetryCount = 0;
        Error = null;
    }
}
