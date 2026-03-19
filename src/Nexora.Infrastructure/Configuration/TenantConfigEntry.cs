namespace Nexora.Infrastructure.Configuration;

public sealed class TenantConfigEntry
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
}
