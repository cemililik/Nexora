namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a contact is GDPR-deleted, enabling cross-module PII cleanup.</summary>
public sealed record ContactGdprDeletedIntegrationEvent : IntegrationEventBase
{
    /// <summary>The erased contact's ID (may no longer exist in DB if hard-deleted).</summary>
    public required Guid ContactId { get; init; }

    /// <summary>Reason for the erasure request (e.g., "data subject request").</summary>
    public required string Reason { get; init; }

    /// <summary>Erasure mode: "anonymized" (soft) or "hard_deleted" (permanent).</summary>
    public required string Mode { get; init; }

    /// <summary>UTC timestamp when the erasure completed.</summary>
    public required DateTime DeletedAtUtc { get; init; }

    /// <summary>The user who initiated/approved the erasure (for audit trail).</summary>
    public required Guid ErasedByUserId { get; init; }
}
