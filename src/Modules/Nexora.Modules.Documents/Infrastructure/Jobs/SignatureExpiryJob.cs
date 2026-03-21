using Microsoft.EntityFrameworkCore;
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
    ITenantContextAccessor tenantContextAccessor,
    DocumentsDbContext dbContext,
    ILogger<SignatureExpiryJob> logger) : NexoraJob<SignatureExpiryJobParams>(tenantContextAccessor, logger)
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(SignatureExpiryJobParams parameters, CancellationToken ct)
    {
        var tenantId = Guid.Parse(parameters.TenantId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expiredRequests = await dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .Where(s => s.TenantId == tenantId
                && s.ExpiresAt != null
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

        logger.LogInformation(
            "Expired {Count} signature requests in tenant {TenantId}",
            expiredRequests.Count, tenantId);
    }
}
