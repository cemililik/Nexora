namespace Nexora.Modules.Reporting.Application.DTOs;

public sealed record ReportDefinitionDto(
    Guid Id,
    string Name,
    string? Description,
    string Module,
    string? Category,
    string QueryText,
    string? Parameters,
    string DefaultFormat,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? CreatedBy);
