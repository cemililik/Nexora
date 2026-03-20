namespace Nexora.Infrastructure.Configuration;

/// <summary>
/// Represents a single tenant configuration key-value entry stored in the database.
/// </summary>
public sealed class TenantConfigEntry
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
}
