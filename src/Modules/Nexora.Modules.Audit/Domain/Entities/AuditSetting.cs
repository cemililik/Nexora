using Nexora.Modules.Audit.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Audit.Domain.Entities;

/// <summary>
/// Configuration entity controlling whether auditing is enabled for a given
/// module/operation pair and how long entries are retained.
/// </summary>
public sealed class AuditSetting : AuditableEntity<AuditSettingId>
{
    public string TenantId { get; private set; } = default!;
    public string Module { get; private set; } = default!;
    public string Operation { get; private set; } = default!;
    public bool IsEnabled { get; private set; }
    public int RetentionDays { get; private set; }
    public string? UpdatedByUser { get; private set; }

    private AuditSetting() { }

    /// <summary>Normalizes module and operation keys to lowercase trimmed form.</summary>
    public static (string Module, string Operation) NormalizeKey(string module, string operation)
        => (module.Trim().ToLowerInvariant(), operation.Trim().ToLowerInvariant());

    /// <summary>Creates a new audit setting for the given tenant, module, and operation.</summary>
    public static AuditSetting Create(
        string tenantId,
        string module,
        string operation,
        bool isEnabled,
        int retentionDays)
    {
        var (normalizedModule, normalizedOperation) = NormalizeKey(module, operation);
        return new AuditSetting
        {
            Id = AuditSettingId.New(),
            TenantId = tenantId,
            Module = normalizedModule,
            Operation = normalizedOperation,
            IsEnabled = isEnabled,
            RetentionDays = retentionDays
        };
    }

    /// <summary>Updates the enabled state and retention period for this setting.</summary>
    public void Update(bool isEnabled, int retentionDays, string updatedBy)
    {
        IsEnabled = isEnabled;
        RetentionDays = retentionDays;
        UpdatedByUser = updatedBy;
    }
}
