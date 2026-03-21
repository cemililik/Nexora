namespace Nexora.Modules.Documents.Application.DTOs;

/// <summary>Summary data transfer object for a document template.</summary>
/// <param name="Id">Template identifier.</param>
/// <param name="Name">Template name.</param>
/// <param name="Category">Template category (Contract, Receipt, Letter, Report).</param>
/// <param name="Format">Output format (Docx, Pdf, Html).</param>
/// <param name="IsActive">Whether the template is currently active.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
public sealed record DocumentTemplateDto(
    Guid Id,
    string Name,
    string Category,
    string Format,
    bool IsActive,
    DateTimeOffset CreatedAt);
