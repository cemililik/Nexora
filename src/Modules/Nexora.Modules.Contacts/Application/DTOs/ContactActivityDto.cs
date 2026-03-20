namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for contact activity timeline entry.</summary>
public sealed record ContactActivityDto(
    Guid Id,
    Guid ContactId,
    string ModuleSource,
    string ActivityType,
    string Summary,
    string? Details,
    DateTimeOffset OccurredAt);
