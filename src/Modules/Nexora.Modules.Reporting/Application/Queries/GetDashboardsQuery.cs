using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetDashboardsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<DashboardDto>>;

public sealed class GetDashboardsHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetDashboardsQuery, PagedResult<DashboardDto>>
{
    public async Task<Result<PagedResult<DashboardDto>>> Handle(GetDashboardsQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Dashboards
            .Where(d => d.TenantId == tenantId)
            .AsNoTracking();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DashboardDto(
                d.Id.Value, d.Name, d.Description, d.IsDefault,
                d.Widgets, d.CreatedAt, d.CreatedBy))
            .ToListAsync(ct);

        return Result<PagedResult<DashboardDto>>.Success(
            new PagedResult<DashboardDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
