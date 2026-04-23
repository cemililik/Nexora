namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// A single permission declared by a module via <see cref="IPermissionRegistry"/>.
/// The registry is the single source of truth — Identity module's seed reads this at
/// tenant-provisioning time and writes rows into <c>identity_permissions</c>.
/// </summary>
/// <param name="Module">First segment of the permission name, lowercase single token (e.g. "crm").</param>
/// <param name="Resource">Second segment — resource noun (e.g. "lead", "pipeline").</param>
/// <param name="Action">Third segment — action verb (e.g. "read", "write", "manage").</param>
/// <param name="DescriptionKey">Localization key for the description shown in the admin UI.</param>
/// <param name="Scope">Platform vs Tenant scope. Defaults to Tenant per permissions.md §2.</param>
/// <remarks>
/// See <c>docs/standards/permissions.md</c> §3 and ADR-004 (Centralized Permission Seeding).
/// </remarks>
public sealed record PermissionDefinition(
    string Module,
    string Resource,
    string Action,
    string DescriptionKey,
    PermissionScope Scope = PermissionScope.Tenant)
{
    /// <summary>Canonical dotted name used in authorization policies and DB rows.</summary>
    public string Name => $"{Module}.{Resource}.{Action}";
}
