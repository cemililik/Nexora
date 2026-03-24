namespace Nexora.Modules.Reporting.Application.DTOs;

public sealed record ReportScheduleDto(
    Guid Id,
    Guid DefinitionId,
    string CronExpression,
    string Format,
    string? Recipients,
    bool IsActive,
    DateTimeOffset? LastExecutionAt,
    DateTimeOffset? NextExecutionAt,
    DateTimeOffset CreatedAt);
