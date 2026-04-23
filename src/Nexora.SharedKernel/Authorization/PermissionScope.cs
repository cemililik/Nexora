namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Classifies a permission by the tier of user that may hold it.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description>
///     <see cref="Platform"/> — permissions reserved for Nexora platform operators (SaaS staff or
///     on-premises Platform Admin). Includes tenant lifecycle management and global module
///     installation. Tenant administrators never receive these permissions.
///   </description></item>
///   <item><description>
///     <see cref="Tenant"/> — permissions that tenant administrators may assign to roles within
///     their own tenant. This is the default scope for all business-module permissions.
///   </description></item>
/// </list>
/// </remarks>
public enum PermissionScope
{
    /// <summary>
    /// Accessible only to Nexora platform operators — <c>identity.tenants.*</c> is the canonical example.
    /// These permissions are never shown in the tenant admin UI and cannot be assigned to tenant roles.
    /// </summary>
    Platform,

    /// <summary>
    /// Standard business-module permissions assignable by tenant administrators within their own tenant.
    /// </summary>
    Tenant
}
