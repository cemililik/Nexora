namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>Summary DTO for contact list views.</summary>
public sealed record ContactDto(
    Guid Id,
    string Type,
    string? Title,
    string? FirstName,
    string? LastName,
    string DisplayName,
    string? CompanyName,
    string? Email,
    string? Phone,
    string Source,
    string Status,
    DateTimeOffset CreatedAt);
