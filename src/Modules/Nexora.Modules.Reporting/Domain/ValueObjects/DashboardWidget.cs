namespace Nexora.Modules.Reporting.Domain.ValueObjects;

/// <summary>
/// Widget configuration stored as JSON in Dashboard entity.
/// </summary>
public sealed record DashboardWidget
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public WidgetType Type { get; init; }
    public string Title { get; init; } = default!;
    public Guid ReportDefinitionId { get; init; }
    public ChartType? ChartType { get; init; }
    public int PositionX { get; init; }
    public int PositionY { get; init; }
    public int SizeW { get; init; } = 1;
    public int SizeH { get; init; } = 1;
    public string? Config { get; init; }
}
