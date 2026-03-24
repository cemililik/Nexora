using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetDashboardWidgetDataQuery(
    Guid DashboardId,
    Guid WidgetId) : IQuery<WidgetDataDto>;

public sealed class GetDashboardWidgetDataHandler(
    ReportingDbContext dbContext,
    ReportExecutionService executionService,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetDashboardWidgetDataQuery, WidgetDataDto>
{
    public async Task<Result<WidgetDataDto>> Handle(GetDashboardWidgetDataQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var dashboardId = DashboardId.From(request.DashboardId);

        var dashboard = await dbContext.Dashboards
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.TenantId == tenantId, ct);

        if (dashboard is null)
            return Result<WidgetDataDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_dashboard_not_found"));

        if (string.IsNullOrEmpty(dashboard.Widgets))
            return Result<WidgetDataDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_widget_not_found"));

        var widgets = JsonSerializer.Deserialize<List<DashboardWidget>>(dashboard.Widgets);
        var widget = widgets?.FirstOrDefault(w => w.Id == request.WidgetId);

        if (widget is null)
            return Result<WidgetDataDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_widget_not_found"));

        var definitionId = ReportDefinitionId.From(widget.ReportDefinitionId);
        var definition = await dbContext.ReportDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.TenantId == tenantId, ct);

        if (definition is null)
            return Result<WidgetDataDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));

        var rows = await executionService.ExecuteQueryAsync(
            tenantId.ToString(), definition.QueryText, null, ct);

        return Result<WidgetDataDto>.Success(
            new WidgetDataDto(widget.Id, widget.Type.ToString(), rows, rows.Count));
    }
}
