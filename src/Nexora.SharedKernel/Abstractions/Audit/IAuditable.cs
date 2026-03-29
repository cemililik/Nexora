namespace Nexora.SharedKernel.Abstractions.Audit;

/// <summary>
/// Optional marker interface for commands that provide explicit audit metadata.
/// When a command implements this interface, its values override the auto-detected
/// module name and operation name derived from the command's namespace and type name.
/// </summary>
public interface IAuditable
{
    /// <summary>The module name for audit logging (e.g., "Contacts").</summary>
    string AuditModule { get; }

    /// <summary>The operation name for audit logging (e.g., "CreateContact").</summary>
    string AuditOperation { get; }

    /// <summary>The entity type affected, if applicable (e.g., "Contact").</summary>
    string? AuditEntityType { get; }

    /// <summary>Explicit operation type override. When null, derived from operation name prefix.</summary>
    OperationType? AuditOperationType => null;
}
