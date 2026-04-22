namespace Nexora.SharedKernel.Domain.Events;

/// <summary>
/// Published when a contact is GDPR-deleted, enabling cross-module PII cleanup.
/// Defaults apply for backward compatibility with events produced before Phase-1.5.6 —
/// new producers MUST populate all fields.
/// </summary>
public sealed record ContactGdprDeletedIntegrationEvent : IntegrationEventBase
{
    /// <summary>The erased contact's ID (may no longer exist in DB if hard-deleted).</summary>
    public required Guid ContactId { get; init; }

    /// <summary>Reason for the erasure request (e.g., "data subject request").</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Erasure mode: "anonymized" (soft) or "hard_deleted" (permanent).
    /// Defaults to "anonymized" so in-flight pre-Phase-1.5.6 messages deserialize cleanly.
    /// </summary>
    public string Mode { get; init; } = "anonymized";

    /// <summary>
    /// UTC timestamp when the erasure completed.
    /// Defaults to <see cref="DateTime.UtcNow"/> for backward compatibility; new producers MUST set this.
    /// </summary>
    public DateTime DeletedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The user who initiated/approved the erasure (for audit trail).
    /// Defaults to <see cref="Guid.Empty"/> for backward compatibility; new producers MUST set this.
    /// </summary>
    public Guid ErasedByUserId { get; init; } = Guid.Empty;
}
