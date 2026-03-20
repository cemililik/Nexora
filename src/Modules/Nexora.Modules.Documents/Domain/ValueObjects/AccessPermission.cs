namespace Nexora.Modules.Documents.Domain.ValueObjects;

/// <summary>Permission level for document access.</summary>
public enum AccessPermission
{
    /// <summary>Read-only access.</summary>
    View,

    /// <summary>Read and write access.</summary>
    Edit,

    /// <summary>Full management access.</summary>
    Manage
}
