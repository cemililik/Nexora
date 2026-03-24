namespace Nexora.Modules.Reporting.Domain.ValueObjects;

public readonly record struct ReportDefinitionId(Guid Value)
{
    public static ReportDefinitionId New() => new(Guid.NewGuid());
    public static ReportDefinitionId From(Guid value) => new(value);
    public static ReportDefinitionId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public readonly record struct ReportExecutionId(Guid Value)
{
    public static ReportExecutionId New() => new(Guid.NewGuid());
    public static ReportExecutionId From(Guid value) => new(value);
    public static ReportExecutionId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public readonly record struct ReportScheduleId(Guid Value)
{
    public static ReportScheduleId New() => new(Guid.NewGuid());
    public static ReportScheduleId From(Guid value) => new(value);
    public static ReportScheduleId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public readonly record struct DashboardId(Guid Value)
{
    public static DashboardId New() => new(Guid.NewGuid());
    public static DashboardId From(Guid value) => new(value);
    public static DashboardId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
