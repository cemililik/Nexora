namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for a duplicate contact candidate with confidence score.</summary>
public sealed record DuplicateContactDto(
    Guid ContactId,
    string DisplayName,
    string? Email,
    string? Phone,
    string Type,
    string Status,
    int Score);
