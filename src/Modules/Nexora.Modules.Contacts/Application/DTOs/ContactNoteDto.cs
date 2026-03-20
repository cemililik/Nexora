namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for contact note.</summary>
public sealed record ContactNoteDto(
    Guid Id,
    Guid ContactId,
    Guid AuthorUserId,
    string Content,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
