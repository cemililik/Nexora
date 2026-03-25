using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetReportDefinitionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Module = null,
    string? Category = null,
    string? Search = null) : IQuery<PagedResult<ReportDefinitionDto>>;

public sealed class GetReportDefinitionsHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetReportDefinitionsQuery, PagedResult<ReportDefinitionDto>>
{
    public async Task<Result<PagedResult<ReportDefinitionDto>>> Handle(GetReportDefinitionsQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.ReportDefinitions
            .Where(d => d.TenantId == tenantId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Module))
            query = query.Where(d => d.Module == request.Module);

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(d => d.Category == request.Category);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(d => d.Name.Contains(request.Search) || (d.Description != null && d.Description.Contains(request.Search)));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new ReportDefinitionDto(
                d.Id.Value, d.Name, d.Description, d.Module, d.Category,
                d.QueryText, d.Parameters, d.DefaultFormat.ToString(),
                d.IsActive, d.CreatedAt, d.CreatedBy))
            .ToListAsync(ct);

        return Result<PagedResult<ReportDefinitionDto>>.Success(
            new PagedResult<ReportDefinitionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
    }
}
