using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Domain.Entities;

/// <summary>
/// Tracks a single execution of a report definition.
/// </summary>
public sealed class ReportExecution : AuditableEntity<ReportExecutionId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public ReportDefinitionId DefinitionId { get; private set; }
    public ReportStatus Status { get; private set; }
    public string? ParameterValues { get; private set; } // JSON
    public string? ResultStorageKey { get; private set; }
    public ReportFormat Format { get; private set; }
    public int? RowCount { get; private set; }
    public long? DurationMs { get; private set; }
    public string? ErrorDetails { get; private set; }
    public string? ExecutedBy { get; private set; }
    public string? HangfireJobId { get; private set; }

    private ReportExecution() { }

    public static ReportExecution Create(
        Guid tenantId,
        ReportDefinitionId definitionId,
        ReportFormat format,
        string? parameterValues,
        string? executedBy)
    {
        return new ReportExecution
        {
            Id = ReportExecutionId.New(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            Status = ReportStatus.Queued,
            Format = format,
            ParameterValues = parameterValues,
            ExecutedBy = executedBy
        };
    }

    public void MarkRunning(string? hangfireJobId = null)
    {
        if (Status != ReportStatus.Queued)
            throw new DomainException("lockey_reporting_error_execution_not_queued");
        Status = ReportStatus.Running;
        HangfireJobId = hangfireJobId;
    }

    public void MarkCompleted(string resultStorageKey, int rowCount, long durationMs)
    {
        if (Status != ReportStatus.Running)
            throw new DomainException("lockey_reporting_error_execution_not_running");
        Status = ReportStatus.Completed;
        ResultStorageKey = resultStorageKey;
        RowCount = rowCount;
        DurationMs = durationMs;
        AddDomainEvent(new ReportExecutionCompletedEvent(Id, DefinitionId));
    }

    public void MarkFailed(string errorDetails, long durationMs)
    {
        if (Status is not (ReportStatus.Queued or ReportStatus.Running))
            throw new DomainException("lockey_reporting_error_execution_already_finished");
        Status = ReportStatus.Failed;
        ErrorDetails = errorDetails;
        DurationMs = durationMs;
    }
}
