using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>Represents a module installed for a tenant.</summary>
public sealed class TenantModule : Entity<TenantModuleId>
{
    public TenantId TenantId { get; private set; }
    public string ModuleName { get; private set; } = default!;
    public DateTimeOffset InstalledAt { get; private set; }
    public string? InstalledBy { get; private set; }
    public bool IsActive { get; private set; } = true;

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
}
