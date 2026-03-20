namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for document summary.</summary>
public sealed record DocumentDto(
    Guid Id,
    Guid FolderId,
    string Name,
    string? Description,
    string MimeType,
    long FileSize,
    string StorageKey,
    string Status,
    Guid? LinkedEntityId,
    string? LinkedEntityType,
    int CurrentVersion,
    DateTimeOffset CreatedAt);
