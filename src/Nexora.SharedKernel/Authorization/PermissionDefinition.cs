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
public sealed record PermissionDefinition
{
    /// <summary>First segment of the permission name, lowercase single token (e.g. "crm").</summary>
    public string Module { get; }
    /// <summary>Second segment — resource noun (e.g. "lead", "pipeline").</summary>
    public string Resource { get; }
    /// <summary>Third segment — action verb (e.g. "read", "write", "manage").</summary>
    public string Action { get; }
    /// <summary>Localization key for the description shown in the admin UI.</summary>
    public string DescriptionKey { get; }
    /// <summary>Platform vs Tenant scope. Defaults to Tenant per permissions.md §2.</summary>
    public PermissionScope Scope { get; }

    /// <summary>
    /// Constructs a <see cref="PermissionDefinition"/>. Validates that each segment is
    /// non-empty and does NOT contain a '.' — the dotted form is the computed
    /// <see cref="Name"/>; pre-dotted segments would produce malformed canonical names
    /// that still parse but point to a different permission than intended.
    /// </summary>
    /// <exception cref="ArgumentException">When any segment is empty, whitespace-only, or contains '.'.</exception>
    public PermissionDefinition(
        string Module,
        string Resource,
        string Action,
        string DescriptionKey,
        PermissionScope Scope = PermissionScope.Tenant)
    {
        ValidateSegment(Module, nameof(Module));
        ValidateSegment(Resource, nameof(Resource));
        ValidateSegment(Action, nameof(Action));
        ArgumentException.ThrowIfNullOrWhiteSpace(DescriptionKey);

        this.Module = Module;
        this.Resource = Resource;
        this.Action = Action;
        this.DescriptionKey = DescriptionKey;
        this.Scope = Scope;
    }

    /// <summary>Canonical dotted name used in authorization policies and DB rows.</summary>
    public string Name => $"{Module}.{Resource}.{Action}";

    private static void ValidateSegment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (value.Contains('.', StringComparison.Ordinal))
            throw new ArgumentException(
                $"Permission segment must not contain '.': '{value}' is invalid.", paramName);
    }
}
