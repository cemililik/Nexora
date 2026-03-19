using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to retrieve a single tenant by ID with installed modules.</summary>
public sealed record GetTenantByIdQuery(Guid TenantId) : IQuery<TenantDetailDto>;

/// <summary>Returns tenant detail including installed modules, or failure if not found.</summary>
public sealed class GetTenantByIdHandler(
    PlatformDbContext platformDb) : IQueryHandler<GetTenantByIdQuery, TenantDetailDto>
{
    public async Task<Result<TenantDetailDto>> Handle(
        GetTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result<TenantDetailDto>.Failure("lockey_identity_error_tenant_not_found");
        }

        var installedModules = await platformDb.TenantModules
            .Where(tm => tm.TenantId == tenantId && tm.IsActive)
            .Select(tm => tm.ModuleName)
            .ToListAsync(cancellationToken);

        var dto = new TenantDetailDto(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenant.RealmId,
            tenant.Settings,
            tenant.CreatedAt,
            installedModules);

        return Result<TenantDetailDto>.Success(dto);
    }
}
