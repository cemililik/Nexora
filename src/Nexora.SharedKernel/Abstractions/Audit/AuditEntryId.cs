namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>Strongly-typed identifier for an audit log entry.</summary>
public readonly record struct AuditEntryId(Guid Value)
{
    /// <summary>Creates a new unique <see cref="AuditEntryId"/>.</summary>
    public static AuditEntryId New() => new(Guid.NewGuid());

    /// <summary>Wraps an existing <see cref="Guid"/> as an <see cref="AuditEntryId"/>.</summary>
    public static AuditEntryId From(Guid value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
