namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for contact relationship.</summary>
public sealed record ContactRelationshipDto(
    Guid Id,
    Guid ContactId,
    Guid RelatedContactId,
    string RelatedContactDisplayName,
    string Type,
    DateTimeOffset CreatedAt);
