using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetDashboardByIdQuery(Guid Id) : IQuery<DashboardDto>;

public sealed class GetDashboardByIdHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDashboardByIdHandler> logger) : IQueryHandler<GetDashboardByIdQuery, DashboardDto>
{
    public async Task<Result<DashboardDto>> Handle(GetDashboardByIdQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var dashboardId = DashboardId.From(request.Id);

        var dashboard = await dbContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.TenantId == tenantId, ct);

        if (dashboard is null)
        {
            logger.LogDebug("Dashboard {DashboardId} not found", request.Id);
            return Result<DashboardDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_dashboard_not_found"));
        }

        return Result<DashboardDto>.Success(
            new DashboardDto(dashboard.Id.Value, dashboard.Name, dashboard.Description,
                dashboard.IsDefault, dashboard.Widgets, dashboard.CreatedAt, dashboard.CreatedBy));
    }
}
