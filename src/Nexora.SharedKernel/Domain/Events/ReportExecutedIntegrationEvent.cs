namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a report execution completes successfully.</summary>
public sealed record ReportExecutedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the report execution identifier.</summary>
    public required Guid ExecutionId { get; init; }

    /// <summary>Gets the report definition identifier.</summary>
    public required Guid DefinitionId { get; init; }

    /// <summary>Gets the name of the executed report.</summary>
    public required string ReportName { get; init; }

    /// <summary>Gets the execution duration in milliseconds.</summary>
    public required long DurationMs { get; init; }
}
