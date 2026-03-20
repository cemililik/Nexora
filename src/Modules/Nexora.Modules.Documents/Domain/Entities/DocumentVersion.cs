using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;

namespace Nexora.Modules.Documents.Domain.Entities;

/// <summary>
/// Represents a specific version of a document, tracking storage key and metadata.
/// </summary>
public sealed class DocumentVersion : AuditableEntity<DocumentVersionId>
{
    /// <summary>Gets the parent document identifier.</summary>
    public DocumentId DocumentId { get; private set; }

    /// <summary>Gets the version number.</summary>
    public int VersionNumber { get; private set; }

    /// <summary>Gets the storage key for this version.</summary>
    public string StorageKey { get; private set; } = default!;

    /// <summary>Gets the file size in bytes.</summary>
    public long FileSize { get; private set; }

    /// <summary>Gets the optional change note for this version.</summary>
    public string? ChangeNote { get; private set; }

    /// <summary>Gets the identifier of the user who uploaded this version.</summary>
    public Guid UploadedByUserId { get; private set; }

    private DocumentVersion() { }

    /// <summary>Creates a new DocumentVersion instance.</summary>
    public static DocumentVersion Create(
        DocumentId documentId,
        int versionNumber,
        string storageKey,
        long fileSize,
        Guid uploadedByUserId,
        string? changeNote = null)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));
        if (versionNumber <= 0)
            throw new ArgumentException("Version number must be positive.", nameof(versionNumber));
        if (fileSize <= 0)
            throw new ArgumentException("File size must be positive.", nameof(fileSize));

        return new DocumentVersion
        {
            Id = DocumentVersionId.New(),
            DocumentId = documentId,
            VersionNumber = versionNumber,
            StorageKey = storageKey.Trim(),
            FileSize = fileSize,
            UploadedByUserId = uploadedByUserId,
            ChangeNote = changeNote?.Trim()
        };
    }
}
