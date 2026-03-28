using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to retrieve a single tenant by ID with installed modules.</summary>
public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<TenantDetailDto>;

/// <summary>Returns tenant detail including installed modules, or failure if not found.</summary>
public sealed class GetTenantByIdHandler(
    PlatformDbContext platformDb,
    ILogger<GetTenantByIdHandler> logger) : IQueryHandler<GetTenantByIdQuery, TenantDetailDto>
{
    private const long SlowQueryThresholdMs = 500;

    public async Task<Result<TenantDetailDto>> Handle(
        GetTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var tenantId = TenantId.From(request.TenantId);

        var tenant = await platformDb.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            logger.LogDebug("Tenant {TenantId} not found", request.TenantId);
            return Result<TenantDetailDto>.Failure(LocalizedMessage.Of("lockey_identity_error_tenant_not_found"));
        }

        var installedModules = await platformDb.TenantModules.AsNoTracking()
            .Where(tm => tm.TenantId == tenantId && tm.IsActive)
            .Select(tm => tm.ModuleName)
            .ToListAsync(cancellationToken);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowQueryThresholdMs)
        {
            logger.LogWarning(
                "Slow query detected: GetTenantById took {ElapsedMs}ms for tenant {TenantId}",
                stopwatch.ElapsedMilliseconds, request.TenantId);
        }

        var dto = new TenantDetailDto(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.RealmId,
            tenant.Settings,
            tenant.CreatedAt,
            installedModules);

        return Result<TenantDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_tenant_retrieved"));
    }
}
