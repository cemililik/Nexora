namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for merge operation result.</summary>
public sealed record MergeResultDto(
    Guid PrimaryContactId,
    Guid SecondaryContactId,
    string PrimaryDisplayName);
