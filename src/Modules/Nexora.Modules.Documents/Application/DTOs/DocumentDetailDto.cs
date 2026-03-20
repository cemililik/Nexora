namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for document detail including versions and access list.</summary>
public sealed record DocumentDetailDto(
    Guid Id,
    Guid FolderId,
    string FolderName,
    string Name,
    string? Description,
    string MimeType,
    long FileSize,
    string StorageKey,
    string Status,
    Guid? LinkedEntityId,
    string? LinkedEntityType,
    int CurrentVersion,
    string? Tags,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<DocumentVersionDto> Versions,
    IReadOnlyList<DocumentAccessDto> AccessList);
