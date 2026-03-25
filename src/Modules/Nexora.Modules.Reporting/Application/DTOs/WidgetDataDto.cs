namespace Nexora.Modules.Reporting.Application.DTOs;

/// <summary>Dynamic data returned for a dashboard widget.</summary>
public sealed record WidgetDataDto(
    Guid WidgetId,
    string WidgetType,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int RowCount);
