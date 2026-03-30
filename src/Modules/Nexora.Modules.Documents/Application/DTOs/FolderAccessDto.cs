namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for a folder access permission.</summary>
public sealed record FolderAccessDto(
    Guid Id,
    Guid? UserId,
    Guid? RoleId,
    string Permission,
    DateTime? ExpiresAt,
    bool IsExpired);
