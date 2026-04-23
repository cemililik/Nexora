namespace Nexora.SharedKernel.Domain.Events;

/// <summary>Published when a contact import job completes processing.</summary>
public sealed record ContactImportCompletedIntegrationEvent : IntegrationEventBase
{
    /// <summary>Gets the import job identifier.</summary>
    public required Guid ImportJobId { get; init; }

    /// <summary>Gets the total number of rows in the import file.</summary>
    public required int TotalRows { get; init; }

    /// <summary>Gets the number of successfully imported rows.</summary>
    public required int SuccessCount { get; init; }

    /// <summary>Gets the number of rows that failed to import (invalid data).</summary>
    public required int ErrorCount { get; init; }

    /// <summary>Gets the number of rows skipped because a matching contact already exists.</summary>
    public int SkippedCount { get; init; }
}
