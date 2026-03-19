namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for consent record.</summary>
public sealed record ConsentRecordDto(
    Guid Id,
    Guid ContactId,
    string ConsentType,
    bool Granted,
    string? Source,
    DateTimeOffset GrantedAt,
    DateTimeOffset? RevokedAt);
