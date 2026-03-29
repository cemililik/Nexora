using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Jobs;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.Jobs;

/// <summary>Parameters for the signature expiry job.</summary>
public sealed record SignatureExpiryJobParams : JobParams;

/// <summary>
/// Recurring job that expires signature requests past their expiration date.
/// Runs daily. Marks expired requests and their pending recipients as expired.
/// </summary>
public sealed class SignatureExpiryJob(
    IActiveTenantProvider tenantProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<SignatureExpiryJob> logger) : PlatformJob<SignatureExpiryJobParams>(tenantProvider, scopeFactory, logger)
{
    protected override string? GetRequiredModule() => "documents";

    /// <inheritdoc />
    protected override async Task ExecuteForTenantAsync(
        SignatureExpiryJobParams parameters, ActiveTenantInfo tenant,
        IServiceProvider scopedServices, CancellationToken ct)
    {
        var dbContext = scopedServices.GetRequiredService<DocumentsDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expiredRequests = await dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .Where(s => s.ExpiresAt != null
                && s.ExpiresAt <= today
                && s.Status != SignatureRequestStatus.Completed
                && s.Status != SignatureRequestStatus.Cancelled
                && s.Status != SignatureRequestStatus.Expired)
            .ToListAsync(ct);

        if (expiredRequests.Count == 0)
            return;

        foreach (var request in expiredRequests)
        {
            request.Expire();
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Expired {Count} signature requests", expiredRequests.Count);
    }
}
