using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Represents an access permission grant for a document to a user or role.
/// </summary>
public sealed class DocumentAccess : AuditableEntity<DocumentAccessId>
{
    /// <summary>Gets the document identifier.</summary>
    public DocumentId DocumentId { get; private set; }

    /// <summary>Gets the user identifier, or null if access is role-based.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Gets the role identifier, or null if access is user-based.</summary>
    public Guid? RoleId { get; private set; }

    /// <summary>Gets the access permission level.</summary>
    public AccessPermission Permission { get; private set; }

    private DocumentAccess() { }

    /// <summary>Creates a new DocumentAccess instance.</summary>
    public static DocumentAccess Create(
        DocumentId documentId,
        Guid? userId,
        Guid? roleId,
        AccessPermission permission)
    {
        if (userId is null && roleId is null)
            throw new DomainException("lockey_documents_error_access_requires_user_or_role");

        return new DocumentAccess
        {
            Id = DocumentAccessId.New(),
            DocumentId = documentId,
            UserId = userId,
            RoleId = roleId,
            Permission = permission
        };
    }

    /// <summary>Updates the permission level.</summary>
    public void UpdatePermission(AccessPermission permission)
    {
        Permission = permission;
    }
}
