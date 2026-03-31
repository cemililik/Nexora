namespace Nexora.SharedKernel.Abstractions.Messaging;

/// <summary>
/// Idempotent consumer guard. Ensures integration events are processed exactly once
/// by tracking EventId in a module-local inbox table.
/// </summary>
public interface IInboxGuard
{
    /// <summary>Returns true if the event was already processed (duplicate).</summary>
    Task<bool> IsAlreadyProcessedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Marks the event as processed. Call within the same transaction as business logic.</summary>
    void MarkAsProcessed(Guid eventId, string eventType);
}
