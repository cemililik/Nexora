namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Append-only audit record for every compliance-configuration write. Persisted in
/// <c>platform_compliance_policy_audit</c> within the tenant schema. Written atomically
/// with the update by <see cref="DatabaseConfigurationResolver"/>.
/// </summary>
/// <remarks>
/// Kept separate from <c>GdprErasureAudit</c>: one records policy toggles (who turned
/// hard-delete on), the other records executions (who ran an erasure). See ADR-0025.
/// </remarks>
public sealed class CompliancePolicyAuditEntry
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>
    /// Organization context of the change. <see langword="null"/> when the change was
    /// made at tenant-default scope (reserved for future tooling; not user-writable today).
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public string Key { get; set; } = default!;

    /// <summary>Value before the change, JSON-encoded. <see langword="null"/> for initial writes.</summary>
    public string? OldValue { get; set; }

    /// <summary>Value after the change, JSON-encoded. <see langword="null"/> when the override was cleared.</summary>
    public string? NewValue { get; set; }

    public Guid ChangedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp of the change. <see cref="DateTimeOffset"/> matches the
    /// <c>timestamptz</c> column type and the project convention for audit timestamps
    /// (see <c>OrgConfigEntry.UpdatedAt</c>, <c>TenantConfigEntry.UpdatedAt</c>).
    /// </summary>
    public DateTimeOffset ChangedAtUtc { get; set; }

    /// <summary>
    /// Free-text justification for the change (max 500 chars). Required — the resolver
    /// validates non-empty reason before calling <c>AppendAudit</c>, so the column is
    /// enforced non-null at the schema level too.
    /// </summary>
    public string Reason { get; set; } = default!;
}
