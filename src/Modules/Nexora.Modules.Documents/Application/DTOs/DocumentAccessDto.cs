namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Data transfer object for a document access permission.</summary>
public sealed record DocumentAccessDto(
    Guid Id,
    Guid? UserId,
    Guid? RoleId,
    string Permission);
