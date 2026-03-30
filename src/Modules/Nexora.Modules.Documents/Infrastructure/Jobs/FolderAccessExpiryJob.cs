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

        var now = DateTime.UtcNow;

        var expiredAccesses = await dbContext.FolderAccesses
            .Where(a => a.ExpiresAt != null && a.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredAccesses.Count == 0)
            return;

        foreach (var access in expiredAccesses)
            dbContext.FolderAccesses.Remove(access);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Soft-deleted {Count} expired folder access grants", expiredAccesses.Count);
    }
}
