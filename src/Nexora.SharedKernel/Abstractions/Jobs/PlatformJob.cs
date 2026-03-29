using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.SharedKernel.Abstractions.Jobs;

/// <summary>
/// Base class for platform-level recurring jobs that operate across ALL active tenants.
/// Unlike <see cref="NexoraJob{TParams}"/> which runs for a single tenant,
/// PlatformJob iterates all active tenants, creating a fresh DI scope per tenant
/// so each gets a correctly-scoped DbContext with the right schema.
/// </summary>
/// <remarks>
/// Each tenant is processed independently — one tenant's failure does not block others.
/// Override <see cref="GetRequiredModule"/> to filter to tenants with a specific module installed.
/// </remarks>
public abstract class PlatformJob<TParams>(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger logger) where TParams : JobParams
{
    /// <summary>
    /// Entry point called by Hangfire. Iterates all active tenants and calls
    /// <see cref="ExecuteForTenantAsync"/> for each.
    /// </summary>
    public async Task RunAsync(TParams parameters, CancellationToken ct)
    {
        var jobName = GetType().Name;
        logger.LogInformation("Platform job {JobName} starting", jobName);

        var moduleName = GetRequiredModule();
        var tenants = moduleName is not null
            ? await tenantProvider.GetActiveTenantsWithModuleAsync(moduleName, ct)
            : await tenantProvider.GetActiveTenantsAsync(ct);

        logger.LogInformation("Platform job {JobName} processing {TenantCount} tenants", jobName, tenants.Count);

        var successCount = 0;
        var failCount = 0;

        foreach (var tenant in tenants)
        {
            try
            {
                // Fresh scope per tenant → fresh DbContext with correct schema
                await using var scope = scopeFactory.CreateAsyncScope();
                var accessor = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
                accessor.SetTenant(tenant.TenantId);

                await ExecuteForTenantAsync(parameters, tenant, scope.ServiceProvider, ct);
                successCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                logger.LogError(ex, "Platform job {JobName} failed for tenant {TenantId}",
                    jobName, tenant.TenantId);
                // Continue to next tenant — don't let one failure block others
            }
        }

        logger.LogInformation(
            "Platform job {JobName} finished — {SuccessCount} succeeded, {FailCount} failed out of {TotalCount} tenants",
            jobName, successCount, failCount, tenants.Count);
    }

    /// <summary>
    /// Override to specify which module must be installed for the tenant to be processed.
    /// Return null to process all active tenants regardless of installed modules.
    /// </summary>
    protected virtual string? GetRequiredModule() => null;

    /// <summary>
    /// Execute job logic for a single tenant. A fresh DI scope is provided with the
    /// correct tenant context already set. Resolve scoped services (DbContext etc.)
    /// from <paramref name="scopedServices"/>.
    /// </summary>
    protected abstract Task ExecuteForTenantAsync(
        TParams parameters,
        ActiveTenantInfo tenant,
        IServiceProvider scopedServices,
        CancellationToken ct);
}
