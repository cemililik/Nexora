using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Microsoft.Extensions.DependencyInjection;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Infrastructure.Jobs;

/// <summary>
/// Hangfire job filter that propagates tenant context into job parameters
/// and restores it when the job executes.
/// </summary>
public sealed class TenantJobFilter(IServiceProvider serviceProvider)
    : IClientFilter, IServerFilter
{
    private const string TenantIdKey = "TenantId";
    private const string OrganizationIdKey = "OrganizationId";

    // Capture tenant context when job is created
    public void OnCreating(CreatingContext context)
    {
        using var scope = serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetService<ITenantContextAccessor>();

        try
        {
            var tenant = accessor?.Current;
            if (tenant is not null)
            {
                context.SetJobParameter(TenantIdKey, tenant.TenantId);
                context.SetJobParameter(OrganizationIdKey, tenant.OrganizationId);
            }
        }
        catch (InvalidOperationException)
        {
            // Tenant context not set — job was enqueued outside a request (e.g. recurring)
        }
    }

    public void OnCreated(CreatedContext context) { }

    // Restore tenant context when job executes
    public void OnPerforming(PerformingContext context)
    {
        var tenantId = context.GetJobParameter<string>(TenantIdKey);
        if (string.IsNullOrEmpty(tenantId))
            return;

        var orgId = context.GetJobParameter<string>(OrganizationIdKey);
        var accessor = serviceProvider.GetRequiredService<ITenantContextAccessor>();
        accessor.SetTenant(tenantId, orgId);
    }

    public void OnPerformed(PerformedContext context) { }
}
