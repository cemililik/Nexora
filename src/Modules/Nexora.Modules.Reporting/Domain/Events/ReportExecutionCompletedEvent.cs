using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Reporting.Domain.Events;

/// <summary>Raised when a report execution completes successfully.</summary>
public sealed record ReportExecutionCompletedEvent(
    ReportExecutionId ExecutionId,
    ReportDefinitionId DefinitionId) : DomainEventBase;
