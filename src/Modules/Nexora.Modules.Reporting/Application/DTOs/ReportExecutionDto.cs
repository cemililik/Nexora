namespace Nexora.Modules.Reporting.Application.DTOs;

public sealed record ReportExecutionDto(
    Guid Id,
    Guid DefinitionId,
    string Status,
    string? ParameterValues,
    string Format,
    int? RowCount,
    long? DurationMs,
    string? ErrorDetails,
    string? ExecutedBy,
    DateTimeOffset CreatedAt);
