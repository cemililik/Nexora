namespace Nexora.Modules.Documents.Domain.ValueObjects;

/// <summary>Status of a document in its lifecycle.</summary>
public enum DocumentStatus
{
    /// <summary>Document is active.</summary>
    Active,

    /// <summary>Document is archived.</summary>
    Archived,

    /// <summary>Document is soft-deleted.</summary>
    Deleted
}
