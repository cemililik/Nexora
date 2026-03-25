using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Reporting.Infrastructure.Jobs;

public sealed record ScheduledReportDispatcherJobParams : JobParams;

/// <summary>
/// Recurring job that checks active report schedules and enqueues executions.
/// </summary>
public sealed class ScheduledReportDispatcherJob(
    ITenantContextAccessor tenantContextAccessor,
    ReportingDbContext dbContext,
    ILogger<ScheduledReportDispatcherJob> logger)
    : NexoraJob<ScheduledReportDispatcherJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(ScheduledReportDispatcherJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var now = DateTimeOffset.UtcNow;

        var dueSchedules = await dbContext.ReportSchedules
            .Where(s => s.TenantId == tenantId && s.IsActive &&
                        (s.NextExecutionAt == null || s.NextExecutionAt <= now))
            .ToListAsync(ct);

        logger.LogInformation(
            "Found {Count} due report schedules for tenant {TenantId}",
            dueSchedules.Count, tenantId);

        foreach (var schedule in dueSchedules)
        {
            var execution = ReportExecution.Create(
                tenantId, schedule.DefinitionId, schedule.Format, null, "system:scheduler");

            await dbContext.ReportExecutions.AddAsync(execution, ct);

            // Simple next execution calculation (would use a proper cron parser in production)
            schedule.RecordExecution(now, now.AddHours(24));

            logger.LogInformation(
                "Enqueued execution {ExecutionId} for scheduled report {ScheduleId}",
                execution.Id, schedule.Id);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
