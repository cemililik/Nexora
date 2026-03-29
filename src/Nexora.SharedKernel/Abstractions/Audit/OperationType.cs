namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Defines the type of operation being audited.
/// </summary>
public enum OperationType
{
    /// <summary>A new entity was created.</summary>
    Create,

    /// <summary>An existing entity was updated.</summary>
    Update,

    /// <summary>An entity was deleted.</summary>
    Delete,

    /// <summary>A custom action was performed.</summary>
    Action,

    /// <summary>A read/query operation was performed.</summary>
    Read
}
