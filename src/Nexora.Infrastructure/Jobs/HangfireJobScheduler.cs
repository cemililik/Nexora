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
        RecurringJob.AddOrUpdate<TJob>(
            jobId,
            queue,
            job => ExecuteJob(job),
            cronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    private static Task ExecuteJob<TJob>(TJob job) where TJob : class
    {
        // The actual job execution is handled by Hangfire's activation.
        // Jobs must implement NexoraJob<TParams>.
        return Task.CompletedTask;
    }
}
