namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for folder summary.</summary>
public sealed record FolderDto(
    Guid Id,
    string Name,
    string Path,
    Guid? ParentFolderId,
    string? ModuleName,
    bool IsSystem,
    DateTimeOffset CreatedAt);
