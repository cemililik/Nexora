namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for GDPR data export for a contact.</summary>
public sealed record GdprExportDto(
    Guid ContactId,
    string DisplayName,
    ContactDetailDto? ContactData,
    IReadOnlyList<ContactNoteDto> Notes,
    IReadOnlyList<ConsentRecordDto> ConsentRecords,
    IReadOnlyList<ContactActivityDto> Activities,
    IReadOnlyList<ContactCustomFieldDto> CustomFields,
    DateTimeOffset ExportedAt);
