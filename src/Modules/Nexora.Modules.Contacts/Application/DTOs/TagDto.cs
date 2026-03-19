namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>Summary DTO for tag list views.</summary>
public sealed record TagDto(
    Guid Id,
    string Name,
    string Category,
    string? Color,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>DTO for contact-tag assignment.</summary>
public sealed record ContactTagDto(
    Guid ContactTagId,
    Guid ContactId,
    Guid TagId,
    string TagName,
    string TagCategory,
    string? TagColor,
    DateTimeOffset AssignedAt);
