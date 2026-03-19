namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for previewing a merge operation before execution.</summary>
public sealed record MergePreviewDto(
    Guid PrimaryContactId,
    string PrimaryDisplayName,
    string? PrimaryEmail,
    string? PrimaryPhone,
    Guid SecondaryContactId,
    string SecondaryDisplayName,
    string? SecondaryEmail,
    string? SecondaryPhone,
    int TagsToTransfer,
    int RelationshipsToTransfer,
    int CustomFieldsToTransfer,
    int CommunicationPreferencesToTransfer);
