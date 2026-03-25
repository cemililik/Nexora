using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record UpdateDashboardCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Widgets,
    bool IsDefault) : ICommand<DashboardDto>;

public sealed class UpdateDashboardValidator : AbstractValidator<UpdateDashboardCommand>
{
    public UpdateDashboardValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("lockey_validation_required")
            .MaximumLength(200).WithMessage("lockey_validation_max_length");
    }
}

public sealed class UpdateDashboardHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateDashboardHandler> logger) : ICommandHandler<UpdateDashboardCommand, DashboardDto>
{
    public async Task<Result<DashboardDto>> Handle(UpdateDashboardCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var dashboardId = DashboardId.From(request.Id);

        var dashboard = await dbContext.Dashboards
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.TenantId == tenantId, ct);

        if (dashboard is null)
            return Result<DashboardDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_dashboard_not_found"));

        dashboard.Update(request.Name, request.Description, request.Widgets, request.IsDefault);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Dashboard {DashboardId} updated for tenant {TenantId}",
            dashboard.Id, tenantId);

        return Result<DashboardDto>.Success(
            new DashboardDto(dashboard.Id.Value, dashboard.Name, dashboard.Description,
                dashboard.IsDefault, dashboard.Widgets, dashboard.CreatedAt, dashboard.CreatedBy),
            LocalizedMessage.Of("lockey_reporting_dashboard_updated"));
    }
}
