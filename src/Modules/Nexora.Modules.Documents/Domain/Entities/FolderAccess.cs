using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Represents an access permission grant for a folder to a user or role.
/// Supports optional expiration for time-limited access.
/// </summary>
public sealed class FolderAccess : AuditableEntity<FolderAccessId>
{
    /// <summary>Gets the folder identifier.</summary>
    public FolderId FolderId { get; private set; }

    /// <summary>Gets the user identifier, or null if access is role-based.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Gets the role identifier, or null if access is user-based.</summary>
    public Guid? RoleId { get; private set; }

    /// <summary>Gets the access permission level.</summary>
    public AccessPermission Permission { get; private set; }

    /// <summary>Gets the expiration date/time, or null if permanent.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    private FolderAccess() { }

    /// <summary>Creates a new FolderAccess instance.</summary>
    public static FolderAccess Create(
        FolderId folderId,
        Guid? userId,
        Guid? roleId,
        AccessPermission permission,
        DateTimeOffset? expiresAt = null)
    {
        if (userId is null && roleId is null)
            throw new DomainException("lockey_documents_error_access_requires_user_or_role");

        return new FolderAccess
        {
            Id = FolderAccessId.New(),
            FolderId = folderId,
            UserId = userId,
            RoleId = roleId,
            Permission = permission,
            ExpiresAt = expiresAt
        };
    }

    /// <summary>Determines whether this access grant has expired.</summary>
    public bool IsExpired() => IsExpired(DateTimeOffset.UtcNow);

    /// <summary>Determines whether this access grant has expired relative to the specified time.</summary>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    /// <summary>Updates the permission level and optionally the expiration date.</summary>
    public void UpdatePermission(AccessPermission newPermission, DateTimeOffset? newExpiresAt = null)
    {
        Permission = newPermission;
        ExpiresAt = newExpiresAt;
    }
}
