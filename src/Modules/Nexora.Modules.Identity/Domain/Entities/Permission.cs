using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Identity.Domain.Entities;

/// <summary>
/// Permission: {module}.{resource}.{action} format.
/// </summary>
public sealed class Permission : Entity<PermissionId>
{
    public string Module { get; private set; } = default!;
    public string Resource { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string? Description { get; private set; }

    /// <summary>Full permission key: module.resource.action</summary>
    public string Key => $"{Module}.{Resource}.{Action}";

    private Permission() { }

    public static Permission Create(string module, string resource, string action, string? description = null)
    {
        return new Permission
        {
            Id = PermissionId.New(),
            Module = module,
            Resource = resource,
            Action = action,
            Description = description
        };
    }
}
