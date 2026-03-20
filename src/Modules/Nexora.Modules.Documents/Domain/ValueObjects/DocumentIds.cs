namespace Nexora.Modules.Documents.Domain.ValueObjects;

/// <summary>Strongly-typed ID representing a folder.</summary>
public readonly record struct FolderId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static FolderId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static FolderId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a document.</summary>
public readonly record struct DocumentId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static DocumentId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static DocumentId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a document version.</summary>
public readonly record struct DocumentVersionId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static DocumentVersionId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static DocumentVersionId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a document access permission record.</summary>
public readonly record struct DocumentAccessId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static DocumentAccessId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static DocumentAccessId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a signature request.</summary>
public readonly record struct SignatureRequestId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static SignatureRequestId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static SignatureRequestId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a signature recipient.</summary>
public readonly record struct SignatureRecipientId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static SignatureRecipientId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static SignatureRecipientId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}

/// <summary>Strongly-typed ID representing a document template.</summary>
public readonly record struct DocumentTemplateId(Guid Value)
{
    /// <summary>Creates a new random identifier.</summary>
    public static DocumentTemplateId New() => new(Guid.NewGuid());
    /// <summary>Wraps an existing Guid value.</summary>
    public static DocumentTemplateId From(Guid value) => new(value);
    /// <summary>Returns the string representation of the identifier.</summary>
    public override string ToString() => Value.ToString();
}
