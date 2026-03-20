using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Notifications.Infrastructure.Jobs;

/// <summary>Parameters for the daily provider counter reset job.</summary>
public sealed record DailyProviderResetJobParams : JobParams;

/// <summary>
/// Recurring job that resets the daily sent counter on all providers.
/// Runs once a day at midnight UTC.
/// </summary>
public sealed class DailyProviderResetJob(
    ITenantContextAccessor tenantContextAccessor,
    NotificationsDbContext dbContext,
    ILogger<DailyProviderResetJob> logger) : NexoraJob<DailyProviderResetJobParams>(tenantContextAccessor, logger)
{
    protected override async Task ExecuteAsync(DailyProviderResetJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);

        var providers = await dbContext.NotificationProviders
            .Where(p => p.TenantId == tenantId && p.SentToday > 0)
            .ToListAsync(ct);

        foreach (var provider in providers)
        {
            provider.ResetDailyCounter();
        }

        if (providers.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Daily counter reset for {Count} providers in tenant {TenantId}",
                providers.Count, tenantId);
        }
    }
}
