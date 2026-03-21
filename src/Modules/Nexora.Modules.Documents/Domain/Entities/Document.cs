using Nexora.Modules.Documents.Domain.Events;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Aggregate root representing a document with versioning, access control, and entity linking.
/// </summary>
public sealed class Document : AuditableEntity<DocumentId>, IAggregateRoot
{
    /// <summary>The maximum number of versions allowed per document.</summary>
    public const int MaxVersionCount = 100;

    /// <summary>Gets the tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Gets the organization identifier.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Gets the folder identifier where the document resides.</summary>
    public FolderId FolderId { get; private set; }

    /// <summary>Gets the identifier of the user who uploaded the document.</summary>
    public Guid UploadedByUserId { get; private set; }

    /// <summary>Gets the document name.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Gets the document description.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the MIME type of the document.</summary>
    public string MimeType { get; private set; } = default!;

    /// <summary>Gets the file size in bytes.</summary>
    public long FileSize { get; private set; }

    /// <summary>Gets the storage key for the current version.</summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>Gets the document status.</summary>
    public DocumentStatus Status { get; private set; }

    /// <summary>Gets the linked entity identifier.</summary>
    public Guid? LinkedEntityId { get; private set; }

    /// <summary>Gets the linked entity type name.</summary>
    public string? LinkedEntityType { get; private set; }

    /// <summary>Gets the current version number.</summary>
    public int CurrentVersion { get; private set; }

    /// <summary>Gets the comma-separated tags.</summary>
    public string? Tags { get; private set; }

    private readonly List<DocumentVersion> _versions = [];

    /// <summary>Gets the collection of document versions.</summary>
    public IReadOnlyList<DocumentVersion> Versions => _versions.AsReadOnly();

    private readonly List<DocumentAccess> _accessList = [];

    /// <summary>Gets the collection of access permissions.</summary>
    public IReadOnlyList<DocumentAccess> AccessList => _accessList.AsReadOnly();

    private Document() { }

    /// <summary>Creates a new Document instance.</summary>
    public static Document Create(
        Guid tenantId,
        Guid organizationId,
        FolderId folderId,
        Guid uploadedByUserId,
        string name,
        string mimeType,
        long fileSize,
        string storageKey,
        string? description = null,
        Guid? linkedEntityId = null,
        string? linkedEntityType = null,
        string? tags = null)
    {
        var document = new Document
        {
            Id = DocumentId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            FolderId = folderId,
            UploadedByUserId = uploadedByUserId,
            Name = name.Trim(),
            Description = description?.Trim(),
            MimeType = mimeType,
            FileSize = fileSize,
            StorageKey = storageKey,
            Status = DocumentStatus.Active,
            LinkedEntityId = linkedEntityId,
            LinkedEntityType = linkedEntityType?.Trim(),
            CurrentVersion = 1,
            Tags = tags
        };
        document.AddDomainEvent(new DocumentCreatedEvent(document.Id, document.Name, document.MimeType, document.FileSize));
        return document;
    }

    /// <summary>Creates a document record pending file rendering from a template.</summary>
    public static Document CreatePendingRender(
        Guid tenantId,
        Guid organizationId,
        FolderId folderId,
        Guid uploadedByUserId,
        string name,
        string mimeType,
        string storageKey,
        DocumentTemplateId templateId,
        string? description = null)
    {
        var document = new Document
        {
            Id = DocumentId.New(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            FolderId = folderId,
            UploadedByUserId = uploadedByUserId,
            Name = name.Trim(),
            Description = description?.Trim(),
            MimeType = mimeType,
            FileSize = 0,
            StorageKey = storageKey,
            Status = DocumentStatus.PendingRender,
            CurrentVersion = 1
        };
        document.AddDomainEvent(new DocumentRenderRequestedEvent(document.Id, templateId));
        return document;
    }

    /// <summary>Adds a new version to the document.</summary>
    public DocumentVersion AddVersion(string storageKey, long fileSize, Guid uploadedByUserId, string? changeNote = null)
    {
        if (CurrentVersion >= MaxVersionCount)
            throw new DomainException("lockey_documents_error_max_versions_exceeded");

        CurrentVersion++;
        var version = DocumentVersion.Create(Id, CurrentVersion, storageKey, fileSize, uploadedByUserId, changeNote);
        _versions.Add(version);
        StorageKey = storageKey;
        FileSize = fileSize;
        AddDomainEvent(new DocumentVersionAddedEvent(Id, CurrentVersion));
        return version;
    }

    /// <summary>Archives the document.</summary>
    public void Archive()
    {
        if (Status is DocumentStatus.Deleted)
            throw new DomainException("lockey_documents_error_cannot_archive_deleted");

        if (Status is DocumentStatus.Archived)
            throw new DomainException("lockey_documents_error_already_archived");

        Status = DocumentStatus.Archived;
        AddDomainEvent(new DocumentArchivedEvent(Id));
    }

    /// <summary>Restores the document from archived status.</summary>
    public void Restore()
    {
        if (Status is not DocumentStatus.Archived)
            throw new DomainException("lockey_documents_error_only_archived_can_restore");

        Status = DocumentStatus.Active;
    }

    /// <summary>Soft-deletes the document.</summary>
    public void SoftDelete()
    {
        Status = DocumentStatus.Deleted;
    }

    /// <summary>Updates the document metadata.</summary>
    public void UpdateMetadata(string name, string? description, string? tags)
    {
        Name = name.Trim();
        Description = description?.Trim();
        Tags = tags;
    }

    /// <summary>Moves the document to another folder.</summary>
    public void MoveToFolder(FolderId folderId)
    {
        FolderId = folderId;
    }

    /// <summary>Links the document to an external entity.</summary>
    public void LinkToEntity(Guid entityId, string entityType)
    {
        LinkedEntityId = entityId;
        LinkedEntityType = entityType.Trim();
    }

    /// <summary>Removes the entity link from the document.</summary>
    public void UnlinkEntity()
    {
        LinkedEntityId = null;
        LinkedEntityType = null;
    }

    /// <summary>Grants access to the document for a user or role.</summary>
    public DocumentAccess GrantAccess(Guid? userId, Guid? roleId, AccessPermission permission)
    {
        if (userId is null && roleId is null)
            throw new DomainException("lockey_documents_error_access_requires_user_or_role");

        var existing = _accessList.FirstOrDefault(a => a.UserId == userId && a.RoleId == roleId);
        if (existing is not null)
            return existing;

        var access = DocumentAccess.Create(Id, userId, roleId, permission);
        _accessList.Add(access);
        AddDomainEvent(new DocumentAccessGrantedEvent(Id, userId, roleId, permission));
        return access;
    }

    /// <summary>Revokes a previously granted access permission.</summary>
    public void RevokeAccess(DocumentAccessId accessId)
    {
        var access = _accessList.FirstOrDefault(a => a.Id == accessId)
            ?? throw new DomainException("lockey_documents_error_access_not_found");
        _accessList.Remove(access);
    }
}
