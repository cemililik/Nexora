using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.Jobs;

/// <summary>Parameters for the folder access expiry cleanup job.</summary>
public sealed record FolderAccessExpiryJobParams : JobParams;

/// <summary>
/// Recurring job that soft-deletes expired folder access grants.
/// Runs daily. Uses Remove() which BaseDbContext converts to soft delete for AuditableEntity.
/// </summary>
public sealed class FolderAccessExpiryJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<FolderAccessExpiryJob> logger) : PlatformJob<FolderAccessExpiryJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "documents";

    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        FolderAccessExpiryJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<DocumentsDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Batch soft-delete: Load only IDs, then remove in chunks to avoid OOM on large datasets.
        // BaseDbContext interceptor converts Remove() to soft delete for AuditableEntity.
        var expiredIds = await dbContext.FolderAccesses
            .Where(a => a.ExpiresAt != null && a.ExpiresAt <= now)
            .Select(a => a.Id)
            .ToListAsync(ct);

        if (expiredIds.Count == 0)
            return;

        const int batchSize = 100;
        for (var i = 0; i < expiredIds.Count; i += batchSize)
        {
            var batch = expiredIds.Skip(i).Take(batchSize).ToList();
            var entities = await dbContext.FolderAccesses
                .Where(a => batch.Contains(a.Id))
                .ToListAsync(ct);

            dbContext.FolderAccesses.RemoveRange(entities);
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Soft-deleted {Count} expired folder access grants for tenant {TenantId}",
            expiredIds.Count, tenant.TenantId);
    }

    /// <summary>Internal test wrapper for <see cref="ExecuteForTenantAsync"/>.</summary>
    internal Task TestExecuteForTenantAsync(
        FolderAccessExpiryJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
        => ExecuteForTenantAsync(parameters, tenant, scopedServices, ct);
}
