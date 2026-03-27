using System.Linq.Expressions;
using Hangfire;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed job scheduler for recurring/scheduled jobs.
/// Uses the static <see cref="RecurringJob"/> API with Expression overloads.
/// </summary>
public sealed class HangfireJobScheduler : IJobScheduler
{
    /// <inheritdoc />
    public void AddOrUpdate<TJob>(
        string jobId,
        string cronExpression,
        Expression<Func<TJob, Task>> methodCall,
        string queue = "default") where TJob : class
    {
        RecurringJob.AddOrUpdate(
            jobId,
            queue,
            methodCall,
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
