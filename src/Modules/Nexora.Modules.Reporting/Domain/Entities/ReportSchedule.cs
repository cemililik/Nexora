using Nexora.Modules.Reporting.Domain.Events;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Domain.Base;
using Nexora.SharedKernel.Domain.Exceptions;

namespace Nexora.Modules.Reporting.Domain.Entities;

/// <summary>
/// Defines a recurring schedule for automatic report execution and email delivery.
/// </summary>
public sealed class ReportSchedule : AuditableEntity<ReportScheduleId>, IAggregateRoot
{
    public Guid TenantId { get; private set; }
    public ReportDefinitionId DefinitionId { get; private set; }
    public string CronExpression { get; private set; } = default!;
    public ReportFormat Format { get; private set; }
    public string? Recipients { get; private set; } // JSON array of email addresses
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastExecutionAt { get; private set; }
    public DateTimeOffset? NextExecutionAt { get; private set; }

    private ReportSchedule() { }

    public static ReportSchedule Create(
        Guid tenantId,
        ReportDefinitionId definitionId,
        string cronExpression,
        ReportFormat format,
        string? recipients)
    {
        var schedule = new ReportSchedule
        {
            Id = ReportScheduleId.New(),
            TenantId = tenantId,
            DefinitionId = definitionId,
            CronExpression = cronExpression.Trim(),
            Format = format,
            Recipients = recipients,
            IsActive = true
        };
        schedule.AddDomainEvent(new ReportScheduleCreatedEvent(schedule.Id, definitionId));
        return schedule;
    }

    public void Update(string cronExpression, ReportFormat format, string? recipients)
    {
        CronExpression = cronExpression.Trim();
        Format = format;
        Recipients = recipients;
    }

    public void RecordExecution(DateTimeOffset executedAt, DateTimeOffset? nextAt)
    {
        LastExecutionAt = executedAt;
        NextExecutionAt = nextAt;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException("lockey_reporting_error_schedule_already_inactive");
        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new DomainException("lockey_reporting_error_schedule_already_active");
        IsActive = true;
    }
}
