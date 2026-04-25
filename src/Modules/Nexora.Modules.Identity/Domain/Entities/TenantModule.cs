using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a module installed for a tenant.</summary>
public sealed class TenantModule : AuditableEntity<TenantModuleId>
{
    public TenantId TenantId { get; private set; }
    public string ModuleName { get; private set; } = default!;
    public DateTimeOffset InstalledAt { get; private set; }
    public string? InstalledBy { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>Comma-separated list of renamed table names after uninstall.</summary>
    public string? DeletedTableNames { get; private set; }

    private TenantModule() { }

    /// <summary>Creates a new tenant module installation record.</summary>
    public static TenantModule Create(TenantId tenantId, string moduleName, string? installedBy = null)
    {
        return new TenantModule
        {
            Id = TenantModuleId.New(),
            TenantId = tenantId,
            ModuleName = moduleName,
            InstalledAt = DateTimeOffset.UtcNow,
            InstalledBy = installedBy
        };
    }

    /// <summary>Deactivates this module installation.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Activates this module installation.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Records the renamed table names during uninstall.</summary>
    public void RecordUninstall(string deletedTableNames)
    {
        if (string.IsNullOrEmpty(deletedTableNames))
            throw new DomainException("lockey_identity_error_deleted_table_names_required");

        DeletedTableNames = deletedTableNames;
    }

    /// <summary>
    /// Parses the CSV-encoded <see cref="DeletedTableNames"/> into a list,
    /// trimming entries and dropping empties. Returned list is empty when
    /// no tables were recorded (e.g. tests against InMemory provider). Used
    /// by the T-025 purge job so CSV splitting is not duplicated inline.
    /// </summary>
    public IReadOnlyList<string> ParseDeletedTableNames()
    {
        if (string.IsNullOrWhiteSpace(DeletedTableNames))
            return [];

        return DeletedTableNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Replaces <see cref="DeletedTableNames"/> with a new CSV after the
    /// purge job has dropped some-but-not-all tables. Pass <c>null</c> when
    /// every entry has been successfully dropped — the column is then
    /// nulled and the row is eligible for hard-delete by the caller.
    /// </summary>
    public void SetDeletedTableNames(string? csv)
    {
        DeletedTableNames = string.IsNullOrWhiteSpace(csv) ? null : csv;
    }
}
