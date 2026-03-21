namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Detailed data transfer object for a document template including variable definitions.</summary>
/// <param name="Id">Template identifier.</param>
/// <param name="Name">Template name.</param>
/// <param name="Category">Template category.</param>
/// <param name="Format">Output format.</param>
/// <param name="TemplateStorageKey">Storage key for the template file.</param>
/// <param name="VariableDefinitions">JSON variable definitions, or null.</param>
/// <param name="IsActive">Whether the template is currently active.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="UpdatedAt">Last update timestamp.</param>
public sealed record DocumentTemplateDetailDto(
    Guid Id,
    string Name,
    string Category,
    string Format,
    string TemplateStorageKey,
    string? VariableDefinitions,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
