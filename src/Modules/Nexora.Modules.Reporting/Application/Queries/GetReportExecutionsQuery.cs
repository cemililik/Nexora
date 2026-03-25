using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetReportExecutionsQuery(
    Guid? DefinitionId = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<ReportExecutionDto>>;

public sealed class GetReportExecutionsHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetReportExecutionsQuery, PagedResult<ReportExecutionDto>>
{
    public async Task<Result<PagedResult<ReportExecutionDto>>> Handle(GetReportExecutionsQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.ReportExecutions
            .Where(e => e.TenantId == tenantId)
            .AsNoTracking();

        if (request.DefinitionId.HasValue)
        {
            var defId = ReportDefinitionId.From(request.DefinitionId.Value);
            query = query.Where(e => e.DefinitionId == defId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReportStatus>(request.Status, true, out var status))
            query = query.Where(e => e.Status == status);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new ReportExecutionDto(
                e.Id.Value, e.DefinitionId.Value, e.Status.ToString(),
                e.ParameterValues, e.Format.ToString(), e.RowCount,
                e.DurationMs, e.ErrorDetails, e.ExecutedBy, e.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<ReportExecutionDto>>.Success(
            new PagedResult<ReportExecutionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
