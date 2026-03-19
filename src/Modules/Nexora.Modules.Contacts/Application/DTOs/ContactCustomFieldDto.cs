namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for a contact's custom field value.</summary>
public sealed record ContactCustomFieldDto(
    Guid Id,
    Guid ContactId,
    Guid FieldDefinitionId,
    string FieldName,
    string FieldType,
    string? Value);
