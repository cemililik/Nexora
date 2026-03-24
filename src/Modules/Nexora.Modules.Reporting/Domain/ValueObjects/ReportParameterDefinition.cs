namespace Nexora.Modules.Reporting.Domain.ValueObjects;

/// <summary>
/// Defines a parameter for a report query. Stored as JSON in ReportDefinition.
/// </summary>
public sealed record ReportParameterDefinition
{
    public string Name { get; init; } = default!;
    public string Type { get; init; } = "String"; // String, Number, Date, Boolean
    public bool Required { get; init; }
    public string? DefaultValue { get; init; }
}
