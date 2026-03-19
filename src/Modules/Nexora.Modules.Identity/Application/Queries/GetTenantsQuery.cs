using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Paginated query to list all tenants.</summary>
public sealed record GetTenantsQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<TenantDto>>;

/// <summary>Returns a paginated list of tenants from the platform database.</summary>
public sealed class GetTenantsHandler(
    PlatformDbContext platformDb) : IQueryHandler<GetTenantsQuery, PagedResult<TenantDto>>
{
    public async Task<Result<PagedResult<TenantDto>>> Handle(
        GetTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var query = platformDb.Tenants.OrderBy(t => t.Name);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TenantDto(
                t.Id.Value,
                t.Name,
                t.Slug,
                t.Status.ToString(),
                t.RealmId,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<TenantDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<TenantDto>>.Success(result);
    }
}
