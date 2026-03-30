using Microsoft.AspNetCore.Authorization;

namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Authorization requirement that demands the authenticated user holds a specific permission.
/// Permission strings follow the <c>{module}.{resource}.{action}</c> convention
/// (e.g., <c>identity.users.read</c>).
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>The permission string the user must possess.</summary>
    public string Permission { get; } = permission;
}
