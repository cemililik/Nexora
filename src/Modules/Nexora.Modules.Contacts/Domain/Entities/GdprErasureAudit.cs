using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Contacts.Domain.Entities;

/// <summary>
/// Append-only audit record for GDPR Article 17 erasure operations.
/// Never updated or soft-deleted — provides a permanent compliance trail even after
/// the underlying contact is hard-deleted.
/// </summary>
public sealed class GdprErasureAudit
{
    /// <summary>Unique identifier for this audit entry.</summary>
    public Guid Id { get; private set; }

    /// <summary>Tenant the erased contact belonged to.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// The original contact identifier. NOT a foreign key — the contact row may
    /// have been hard-deleted in the same transaction.
    /// </summary>
    public Guid ContactId { get; private set; }

    /// <summary>The user who requested or approved the erasure.</summary>
    public Guid ErasedByUserId { get; private set; }

    /// <summary>UTC timestamp when the erasure completed.</summary>
    public DateTime ErasedAtUtc { get; private set; }

    /// <summary>Reason for the erasure (max 500 chars).</summary>
    public string Reason { get; private set; } = default!;

    /// <summary>Erasure mode: "anonymized" or "hard_deleted".</summary>
    public string Mode { get; private set; } = default!;

    /// <summary>JSON map of child-entity row counts affected (for forensics).</summary>
    public string ChildCountsJson { get; private set; } = default!;

    private GdprErasureAudit() { }

    /// <summary>Creates a new append-only audit entry.</summary>
    public static GdprErasureAudit Create(
        Guid tenantId,
        Guid contactId,
        Guid erasedByUserId,
        string reason,
        string mode,
        string childCountsJson)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("lockey_contacts_error_gdpr_audit_reason_required");
        if (mode is not ("anonymized" or "hard_deleted"))
            throw new DomainException("lockey_contacts_error_gdpr_audit_invalid_mode");

        return new GdprErasureAudit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            ErasedByUserId = erasedByUserId,
            ErasedAtUtc = DateTime.UtcNow,
            Reason = reason,
            Mode = mode,
            ChildCountsJson = childCountsJson
        };
    }
}
