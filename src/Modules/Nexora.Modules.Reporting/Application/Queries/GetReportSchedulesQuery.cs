using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetReportSchedulesQuery(
    Guid? DefinitionId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReportScheduleDto>>;

public sealed class GetReportSchedulesHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetReportSchedulesQuery, PagedResult<ReportScheduleDto>>
{
    public async Task<Result<PagedResult<ReportScheduleDto>>> Handle(GetReportSchedulesQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.ReportSchedules
            .Where(s => s.TenantId == tenantId)
            .AsNoTracking();

        if (request.DefinitionId.HasValue)
        {
            var defId = ReportDefinitionId.From(request.DefinitionId.Value);
            query = query.Where(s => s.DefinitionId == defId);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new ReportScheduleDto(
                s.Id.Value, s.DefinitionId.Value, s.CronExpression, s.Format.ToString(),
                s.Recipients, s.IsActive, s.LastExecutionAt, s.NextExecutionAt, s.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<ReportScheduleDto>>.Success(
            new PagedResult<ReportScheduleDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
