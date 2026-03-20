namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for a document version.</summary>
public sealed record DocumentVersionDto(
    Guid Id,
    int VersionNumber,
    string StorageKey,
    long FileSize,
    string? ChangeNote,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAt);
