namespace Nexora.Modules.Contacts.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a contact export job.</summary>
public readonly record struct ExportJobId(Guid Value)
{
    /// <summary>Creates a new random export job ID.</summary>
    public static ExportJobId New() => new(Guid.NewGuid());

    /// <summary>Creates an ID from an existing <see cref="Guid"/>.</summary>
    public static ExportJobId From(Guid value) => new(value);

    /// <summary>Parses an ID from its string representation.</summary>
    public static ExportJobId Parse(string value) => new(Guid.Parse(value));

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
