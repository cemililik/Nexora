using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Audit.Infrastructure.Jobs;

/// <summary>Parameters for the audit cleanup job.</summary>
public sealed record AuditCleanupJobParams : JobParams
{
    /// <summary>Default retention window applied to modules/operations that have no explicit setting.</summary>
    public int DefaultRetentionDays { get; init; } = 365;
}

/// <summary>
/// Weekly recurring job that deletes audit entries older than the per-module/operation retention
/// policy stored in <c>audit_settings</c>. When no setting is configured, entries older than
/// <see cref="AuditCleanupJobParams.DefaultRetentionDays"/> are removed.
/// </summary>
/// <remarks>
/// Runs once per active tenant (PlatformJob fan-out). Bulk deletes via <c>ExecuteDeleteAsync</c>
/// so no entities are materialized in memory. Safe to re-run — idempotent.
/// </remarks>
public sealed class AuditCleanupJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<AuditCleanupJob> logger) : PlatformJob<AuditCleanupJobParams>(tenantProvider, scopeFactory, logger)
{
    /// <inheritdoc />
    protected override string? GetRequiredModule() => "audit";

    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        AuditCleanupJobParams parameters,
        ActiveTenantInfo tenant,
        IServiceProvider scopedServices,
        CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<AuditDbContext>();
        var now = DateTimeOffset.UtcNow;

        // Per-module/operation retention from audit_settings.
        var settings = await dbContext.AuditSettings.AsNoTracking().ToListAsync(ct);
        var totalDeleted = 0;

        foreach (var setting in settings)
        {
            if (setting.RetentionDays <= 0)
                continue;

            var cutoff = now.AddDays(-setting.RetentionDays);
            var deleted = await dbContext.AuditEntries
                .Where(e => e.Module == setting.Module
                         && e.Operation == setting.Operation
                         && e.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                logger.LogInformation(
                    "Audit cleanup: tenant={TenantId} module={Module} op={Operation} removed={Count} cutoff={Cutoff}",
                    tenant.TenantId, setting.Module, setting.Operation, deleted, cutoff);
                totalDeleted += deleted;
            }
        }

        // Catch-all fallback for entries whose (module, operation) has no AuditSetting row.
        var fallbackCutoff = now.AddDays(-parameters.DefaultRetentionDays);
        var settingKeys = settings
            .Select(s => new { s.Module, s.Operation })
            .ToHashSet();

        if (settingKeys.Count == 0)
        {
            var fallbackDeleted = await dbContext.AuditEntries
                .Where(e => e.Timestamp < fallbackCutoff)
                .ExecuteDeleteAsync(ct);
            totalDeleted += fallbackDeleted;
        }
        else
        {
            // EF can't translate ToHashSet.Contains on anonymous types — project to parallel lists.
            var modules = settingKeys.Select(k => k.Module).ToArray();
            var operations = settingKeys.Select(k => k.Operation).ToArray();

            var fallbackDeleted = await dbContext.AuditEntries
                .Where(e => e.Timestamp < fallbackCutoff
                         && !modules.Contains(e.Module)
                         && !operations.Contains(e.Operation))
                .ExecuteDeleteAsync(ct);
            totalDeleted += fallbackDeleted;
        }

        logger.LogInformation(
            "Audit cleanup finished for tenant {TenantId}: total removed={Count}",
            tenant.TenantId, totalDeleted);
    }
}
