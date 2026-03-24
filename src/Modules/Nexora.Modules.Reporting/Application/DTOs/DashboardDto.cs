namespace Nexora.Modules.Reporting.Application.DTOs;

public sealed record DashboardDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    string? Widgets,
    DateTimeOffset CreatedAt,
    string? CreatedBy);
