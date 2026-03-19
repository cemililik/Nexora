namespace Nexora.Modules.Contacts.Application.DTOs;

/// <summary>DTO for custom field definition.</summary>
public sealed record CustomFieldDefinitionDto(
    Guid Id,
    string FieldName,
    string FieldType,
    string? Options,
    bool IsRequired,
    int DisplayOrder,
    bool IsActive,
    DateTimeOffset CreatedAt);
