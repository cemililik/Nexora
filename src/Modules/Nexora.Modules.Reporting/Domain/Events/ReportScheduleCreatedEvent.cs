using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Reporting.Domain.Events;

/// <summary>Raised when a new report schedule is created.</summary>
public sealed record ReportScheduleCreatedEvent(
    ReportScheduleId ScheduleId,
    ReportDefinitionId DefinitionId) : DomainEventBase;
