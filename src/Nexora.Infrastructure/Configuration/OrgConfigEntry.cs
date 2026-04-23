namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Organization-scope override for a configuration key. Persisted in
/// <c>platform_org_config</c> within each tenant schema. Managed exclusively by
/// <see cref="DatabaseConfigurationResolver"/> — module code must go through the
/// resolver, not this entity directly.
/// </summary>
public sealed class OrgConfigEntry
{
    /// <summary>Organization that owns this override.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Configuration key (e.g. <c>gdpr.hard_delete.enabled</c>).</summary>
    public string Key { get; set; } = default!;

    /// <summary>JSON-encoded value (jsonb column).</summary>
    public string Value { get; set; } = default!;

    /// <summary>UTC timestamp of the last write.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Identifier of the user who last wrote this override. Nullable to preserve
    /// forward-compatibility with system-initiated seeds.
    /// </summary>
    public string? UpdatedBy { get; set; }
}
