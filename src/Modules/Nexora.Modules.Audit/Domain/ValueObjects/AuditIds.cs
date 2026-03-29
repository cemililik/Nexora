namespace Nexora.Modules.Audit.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing an audit log entry.</summary>
public readonly record struct AuditEntryId(Guid Value)
{
    public static AuditEntryId New() => new(Guid.NewGuid());
    public static AuditEntryId From(Guid value) => new(value);
    public static AuditEntryId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing an audit setting.</summary>
public readonly record struct AuditSettingId(Guid Value)
{
    public static AuditSettingId New() => new(Guid.NewGuid());
    public static AuditSettingId From(Guid value) => new(value);
    public static AuditSettingId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
