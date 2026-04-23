namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a GDPR erasure audit entry.</summary>
public readonly record struct GdprErasureAuditId(Guid Value)
{
    /// <summary>Creates a new random audit ID.</summary>
    public static GdprErasureAuditId New() => new(Guid.NewGuid());

    /// <summary>Creates an ID from an existing <see cref="Guid"/>.</summary>
    public static GdprErasureAuditId From(Guid value) => new(value);

    /// <summary>Parses an ID from its string representation.</summary>
    public static GdprErasureAuditId Parse(string value) => new(Guid.Parse(value));

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
