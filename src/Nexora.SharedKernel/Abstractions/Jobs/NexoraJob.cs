using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.SharedKernel.Abstractions.Jobs;

/// <summary>
/// Base class for all Nexora background jobs. Provides tenant context, logging, and telemetry.
/// All jobs MUST extend this class.
/// </summary>
public abstract class NexoraJob<TParams>(
    ITenantContextAccessor tenantContextAccessor,
    ILogger logger) where TParams : JobParams
{
    /// <summary>
    /// Entry point called by Hangfire. Sets tenant context then delegates to ExecuteAsync.
    /// </summary>
    public async Task RunAsync(TParams parameters, CancellationToken ct)
    {
        var jobName = GetType().Name;
        tenantContextAccessor.SetTenant(parameters.TenantId, parameters.OrganizationId);

        logger.LogInformation("Job {JobName} starting for tenant {TenantId}", jobName, parameters.TenantId);

        try
        {
            await ExecuteAsync(parameters, ct);
            logger.LogInformation("Job {JobName} completed for tenant {TenantId}", jobName, parameters.TenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobName} failed for tenant {TenantId}", jobName, parameters.TenantId);
            throw;
        }
    }

    /// <summary>
    /// Override this to implement the job logic. Tenant context is already set.
    /// </summary>
    protected abstract Task ExecuteAsync(TParams parameters, CancellationToken ct);
}

/// <summary>
/// Job parameters that include tenant context for tenant-aware execution.
/// </summary>
public abstract record JobParams
{
    public required string TenantId { get; init; }
    public string? OrganizationId { get; init; }
}
