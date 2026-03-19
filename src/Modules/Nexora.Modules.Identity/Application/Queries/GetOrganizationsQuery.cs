using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

public sealed record GetOrganizationsQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<OrganizationDto>>;

public sealed class GetOrganizationsHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetOrganizationsQuery, PagedResult<OrganizationDto>>
{
    public async Task<Result<PagedResult<OrganizationDto>>> Handle(
        GetOrganizationsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Organizations
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OrganizationDto(
                o.Id.Value,
                o.Name,
                o.Slug,
                o.LogoUrl,
                o.Timezone,
                o.DefaultCurrency,
                o.DefaultLanguage,
                o.IsActive))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<OrganizationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<OrganizationDto>>.Success(result);
    }
}
