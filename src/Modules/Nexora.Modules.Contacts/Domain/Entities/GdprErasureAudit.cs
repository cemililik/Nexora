using Nexora.Modules.Contacts.Domain.ValueObjects;
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
    public GdprErasureAuditId Id { get; private set; }

    /// <summary>Tenant the erased contact belonged to.</summary>
    /// <remarks>
    /// Stored as a primitive <see cref="Guid"/> because tenant identifiers are platform-wide
    /// (cross-module) and not represented as a module-level value object in this codebase.
    /// </remarks>
    public Guid TenantId { get; private set; }

    /// <summary>
    /// The original contact identifier. NOT a foreign key — the contact row may
    /// have been hard-deleted in the same transaction, so this entry must be able to
    /// outlive the <c>Contact</c> aggregate. Kept as a raw <see cref="Guid"/> rather than
    /// the module's <c>ContactId</c> value object precisely because the referenced
    /// contact may no longer exist.
    /// </summary>
    public Guid ContactId { get; private set; }

    /// <summary>
    /// The user who requested or approved the erasure. Raw <see cref="Guid"/> — the
    /// Identity module's <c>UserId</c> value object is not visible from Contacts.
    /// </summary>
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
        if (tenantId == Guid.Empty)
            throw new DomainException("lockey_contacts_error_gdpr_audit_tenant_required");
        if (contactId == Guid.Empty)
            throw new DomainException("lockey_contacts_error_gdpr_audit_contact_required");
        if (erasedByUserId == Guid.Empty)
            throw new DomainException("lockey_contacts_error_gdpr_audit_user_required");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("lockey_contacts_error_gdpr_audit_reason_required");
        if (reason.Length > 500)
            throw new DomainException("lockey_contacts_error_gdpr_audit_reason_too_long");
        if (mode is not ("anonymized" or "hard_deleted"))
            throw new DomainException("lockey_contacts_error_gdpr_audit_invalid_mode");
        if (string.IsNullOrWhiteSpace(childCountsJson))
            throw new DomainException("lockey_contacts_error_gdpr_audit_child_counts_required");

        return new GdprErasureAudit
        {
            Id = GdprErasureAuditId.New(),
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
