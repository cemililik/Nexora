using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<DailyProviderResetJob> logger) : PlatformJob<DailyProviderResetJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "notifications";

    protected override async Task ExecuteForTenantAsync(
        DailyProviderResetJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<NotificationsDbContext>();

        var providers = await dbContext.NotificationProviders
            .Where(p => p.SentToday > 0)
            .ToListAsync(ct);

        foreach (var provider in providers)
        {
            provider.ResetDailyCounter();
        }

        if (providers.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Daily counter reset for {Count} providers", providers.Count);
        }
    }
}
