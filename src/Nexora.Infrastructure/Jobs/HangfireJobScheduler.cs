using Hangfire;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed job scheduler for recurring/scheduled jobs.
/// </summary>
public sealed class HangfireJobScheduler : IJobScheduler
{
    /// <inheritdoc />
    public void AddOrUpdate<TJob>(
        string jobId,
        string cronExpression,
        string queue = "default") where TJob : class
    {
        // Hangfire requires the lambda to call an instance method on TJob.
        // ToString() is always available and serves as a no-op placeholder.
        RecurringJob.AddOrUpdate<TJob>(
            jobId,
            queue,
            job => job.ToString(),
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
