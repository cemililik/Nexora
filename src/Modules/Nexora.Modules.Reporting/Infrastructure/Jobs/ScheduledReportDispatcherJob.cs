using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledReportDispatcherJob> logger)
    : PlatformJob<ScheduledReportDispatcherJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "reporting";

    protected override async Task ExecuteForTenantAsync(
        ScheduledReportDispatcherJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<ReportingDbContext>();

        var now = DateTimeOffset.UtcNow;

        var dueSchedules = await dbContext.ReportSchedules
            .Where(s => s.IsActive &&
                        (s.NextExecutionAt == null || s.NextExecutionAt <= now))
            .ToListAsync(ct);

        logger.LogInformation("Found {Count} due report schedules", dueSchedules.Count);

        foreach (var schedule in dueSchedules)
        {
            var execution = ReportExecution.Create(
                schedule.TenantId, schedule.DefinitionId, schedule.Format, null, "system:scheduler");

            await dbContext.ReportExecutions.AddAsync(execution, ct);

            DateTimeOffset? nextAt = null;
            try
            {
                var cron = CronExpression.Parse(schedule.CronExpression);
                var nextOccurrence = cron.GetNextOccurrence(now.UtcDateTime, TimeZoneInfo.Utc);
                nextAt = nextOccurrence.HasValue
                    ? new DateTimeOffset(nextOccurrence.Value, TimeSpan.Zero)
                    : null;
            }
            catch (CronFormatException ex)
            {
                logger.LogWarning(
                    ex,
                    "Invalid cron expression {CronExpression} for schedule {ScheduleId}, skipping next execution calculation",
                    schedule.CronExpression, schedule.Id);
            }

            schedule.RecordExecution(now, nextAt);

            logger.LogInformation(
                "Enqueued execution {ExecutionId} for scheduled report {ScheduleId}",
                execution.Id, schedule.Id);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
