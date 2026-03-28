using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to get organization detail by ID.</summary>
public sealed record GetOrganizationByIdQuery(Guid OrganizationId) : IQuery<OrganizationDetailDto>;

/// <summary>Returns organization detail with member count for the current tenant.</summary>
public sealed class GetOrganizationByIdHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetOrganizationByIdHandler> logger) : IQueryHandler<GetOrganizationByIdQuery, OrganizationDetailDto>
{
    public async Task<Result<OrganizationDetailDto>> Handle(
        GetOrganizationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);

        var org = await dbContext.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (org is null)
        {
            logger.LogDebug("Organization {OrganizationId} not found in tenant {TenantId}", request.OrganizationId, tenantId);
            return Result<OrganizationDetailDto>.Failure(LocalizedMessage.Of("lockey_identity_error_org_not_found"));
        }

        var memberCount = await dbContext.OrganizationUsers.AsNoTracking()
            .CountAsync(ou => ou.OrganizationId == orgId, cancellationToken);

        var dto = new OrganizationDetailDto(
            org.Id.Value, org.Name, org.Slug, org.LogoUrl,
            org.Timezone, org.DefaultCurrency, org.DefaultLanguage,
            org.IsActive, memberCount);

        return Result<OrganizationDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_org_retrieved"));
    }
}
