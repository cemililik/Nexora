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
    /// Constructs a <see cref="PermissionDefinition"/>. Validates each segment against the
    /// canonical shape from <c>docs/standards/permissions.md</c> §1: lowercase
    /// letters/digits with <c>_</c> / <c>-</c> allowed inside (no whitespace, no dots,
    /// no uppercase, no leading digit). <see cref="DescriptionKey"/> must be a valid
    /// lockey — i.e. begin with <c>lockey_</c> — so backend responses can only surface
    /// translatable messages.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When a segment violates the canonical shape or the description key is missing /
    /// whitespace / does not start with <c>lockey_</c>.
    /// </exception>
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
        ValidateDescriptionKey(DescriptionKey, nameof(DescriptionKey));

        this.Module = Module;
        this.Resource = Resource;
        this.Action = Action;
        this.DescriptionKey = DescriptionKey;
        this.Scope = Scope;
    }

    /// <summary>Canonical dotted name used in authorization policies and DB rows.</summary>
    public string Name => $"{Module}.{Resource}.{Action}";

    // Accepts lowercase letters followed by lowercase alphanumerics, `_` or `-`.
    // Mirrors the permission naming rules in permissions.md §1 while allowing
    // existing project tokens like `custom-field` (resource) and `settings_manage`
    // (action with underscore) to validate cleanly.
    private static readonly System.Text.RegularExpressions.Regex SegmentPattern =
        new("^[a-z][a-z0-9_-]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static void ValidateSegment(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!SegmentPattern.IsMatch(value))
            throw new ArgumentException(
                $"Permission segment '{value}' is invalid: must match [a-z][a-z0-9_-]* " +
                "(lowercase, no whitespace, no '.'). See permissions.md §1.",
                paramName);
    }

    private static void ValidateDescriptionKey(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (!value.StartsWith("lockey_", StringComparison.Ordinal))
            throw new ArgumentException(
                $"Permission DescriptionKey '{value}' must start with 'lockey_' — " +
                "backend responses surface translation keys, not literal strings. " +
                "See localization.md.",
                paramName);
    }
}
