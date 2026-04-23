namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Central registry of every permission name the platform knows about. Modules populate
/// it during <c>IModule.OnStartupAsync</c>; the Identity module's migration reads it to
/// seed <c>identity_permissions</c> at tenant provisioning time.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>docs/standards/permissions.md</c> §3 and ADR-004, every permission is declared
/// in exactly one place — the module that owns the resource. Registrations outside the
/// owning module are a module-boundary violation and will be caught by the architecture
/// test <c>PermissionRegistryBoundaryTests</c>.
/// </para>
/// <para>
/// Registered as a singleton. Mutated only during host startup; read-only thereafter.
/// Duplicate registrations throw <see cref="InvalidOperationException"/> — if two modules
/// try to own the same name, exactly one of them is wrong.
/// </para>
/// </remarks>
public interface IPermissionRegistry
{
    /// <summary>
    /// Declares a permission. Throws <see cref="InvalidOperationException"/> if the same
    /// <c>{Module}.{Resource}.{Action}</c> name has already been registered by any module.
    /// </summary>
    /// <param name="module">Module identifier (lowercase, single token).</param>
    /// <param name="resource">Resource noun.</param>
    /// <param name="action">Action verb.</param>
    /// <param name="descriptionKey">Lockey for the UI-facing description.</param>
    /// <param name="scope">Platform or Tenant; defaults to Tenant.</param>
    /// <returns>The stored <see cref="PermissionDefinition"/> — useful for chained seeds.</returns>
    PermissionDefinition Register(
        string module,
        string resource,
        string action,
        string descriptionKey,
        PermissionScope scope = PermissionScope.Tenant);

    /// <summary>Returns <see langword="true"/> when <paramref name="name"/> has been registered.</summary>
    bool IsRegistered(string name);

    /// <summary>
    /// O(1) lookup by canonical name. Returns <see langword="true"/> and populates
    /// <paramref name="definition"/> when the name is registered; otherwise returns
    /// <see langword="false"/> and leaves <paramref name="definition"/> null.
    /// </summary>
    bool TryGetByName(string name, out PermissionDefinition? definition);

    /// <summary>All registered definitions, in registration order.</summary>
    IReadOnlyList<PermissionDefinition> GetAll();

    /// <summary>Returns definitions for a single module, in registration order.</summary>
    IReadOnlyList<PermissionDefinition> GetByModule(string module);

    /// <summary>Filters by scope (Platform or Tenant).</summary>
    IReadOnlyList<PermissionDefinition> GetByScope(PermissionScope scope);
}
