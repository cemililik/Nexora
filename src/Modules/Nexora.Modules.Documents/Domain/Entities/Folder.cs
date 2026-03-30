using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Aggregate root representing a folder in the document hierarchy.
/// Supports nested folders, system folders, and module-scoped folders.
/// </summary>
public sealed class Folder : AuditableEntity<FolderId>, IAggregateRoot
{
    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization identifier.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the parent folder identifier, or null if this is a root folder.</summary>
    public FolderId? ParentFolderId { get; private set; }

    /// <summary>Gets the folder name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the full hierarchical path of the folder.</summary>
    public string Path { get; private set; } = default!;

    /// <summary>Gets the referenced module entity identifier.</summary>
    public Guid? ModuleRef { get; private set; }

    /// <summary>Gets the name of the module this folder is scoped to.</summary>
    public string? ModuleName { get; private set; }

    /// <summary>Gets the owner user identifier.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Gets a value indicating whether this is a system folder.</summary>
    public bool IsSystem { get; private set; }

    private readonly List<FolderAccess> _accessList = [];

    /// <summary>Gets the collection of access permissions.</summary>
    public IReadOnlyList<FolderAccess> AccessList => _accessList.AsReadOnly();

    private Folder() { }

    /// <summary>Creates a new Folder instance.</summary>
    public static Folder Create(
        Guid tenantId,
        Guid organizationId,
        string name,
        Guid ownerUserId,
        string? parentPath = null,
        FolderId? parentFolderId = null,
        string? moduleName = null,
        Guid? moduleRef = null,
        bool isSystem = false)
    {
        if (name.Contains('/') || name.Contains('\\'))
            throw new DomainException("lockey_documents_error_folder_name_invalid_characters");

        var folder = new Folder
        {
            Id = FolderId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Path = string.IsNullOrEmpty(parentPath) ? $"/{name.Trim()}" : $"{parentPath}/{name.Trim()}",
            OwnerUserId = ownerUserId,
            ParentFolderId = parentFolderId,
            ModuleName = moduleName,
            ModuleRef = moduleRef,
            IsSystem = isSystem
        };
        folder.AddDomainEvent(new FolderCreatedEvent(folder.Id, folder.Name));
        return folder;
    }

    /// <summary>Renames the folder.</summary>
    public void Rename(string newName)
    {
        if (IsSystem)
            throw new DomainException("lockey_documents_error_cannot_rename_system_folder");

        Name = newName.Trim();
        // Path will be updated by the handler to cascade changes
    }

    /// <summary>Updates the folder path.</summary>
    public void UpdatePath(string newPath)
    {
        if (IsSystem)
            throw new DomainException("lockey_documents_error_cannot_rename_system_folder");

        Path = newPath;
    }

    /// <summary>Moves the folder to a new parent.</summary>
    public void MoveTo(FolderId? newParentId, string newPath)
    {
        if (IsSystem)
            throw new DomainException("lockey_documents_error_cannot_move_system_folder");

        ParentFolderId = newParentId;
        Path = newPath;
    }

    /// <summary>Grants access to the folder for a user or role.</summary>
    public FolderAccess GrantAccess(Guid? userId, Guid? roleId, AccessPermission permission, DateTime? expiresAt = null)
    {
        if (userId is null && roleId is null)
            throw new DomainException("lockey_documents_error_access_requires_user_or_role");

        var existing = _accessList.FirstOrDefault(a => a.UserId == userId && a.RoleId == roleId && !a.IsDeleted);
        if (existing is not null)
        {
            // Idempotent: same permission → return existing; different permission → update
            if (existing.Permission != permission)
                existing.UpdatePermission(permission, expiresAt);
            return existing;
        }

        var access = FolderAccess.Create(Id, userId, roleId, permission, expiresAt);
        _accessList.Add(access);
        return access;
    }

    /// <summary>Revokes a previously granted access permission.</summary>
    public void RevokeAccess(FolderAccessId accessId)
    {
        var access = _accessList.FirstOrDefault(a => a.Id == accessId)
            ?? throw new DomainException("lockey_documents_error_access_not_found");
        _accessList.Remove(access);
    }
}
