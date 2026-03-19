using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

public sealed class TenantModule : Entity<TenantModuleId>
{
    public TenantId TenantId { get; private set; }
    public string ModuleName { get; private set; } = default!;
    public DateTimeOffset InstalledAt { get; private set; }
    public string? InstalledBy { get; private set; }
    public bool IsActive { get; private set; } = true;

    private TenantModule() { }

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

    public void Deactivate() => IsActive = false;
}
