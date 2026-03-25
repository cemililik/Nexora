using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record DeleteDashboardCommand(Guid Id) : ICommand;

/// <summary>Validates dashboard deletion input.</summary>
public sealed class DeleteDashboardValidator : AbstractValidator<DeleteDashboardCommand>
{
    public DeleteDashboardValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class DeleteDashboardHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteDashboardHandler> logger) : ICommandHandler<DeleteDashboardCommand>
{
    public async Task<Result> Handle(DeleteDashboardCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var dashboardId = DashboardId.From(request.Id);

        var dashboard = await dbContext.Dashboards
            .FirstOrDefaultAsync(d => d.Id == dashboardId && d.TenantId == tenantId, ct);

        if (dashboard is null)
            return Result.Failure(LocalizedMessage.Of("lockey_reporting_error_dashboard_not_found"));

        dbContext.Dashboards.Remove(dashboard);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Dashboard {DashboardId} deleted for tenant {TenantId}",
            request.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_reporting_dashboard_deleted"));
    }
}
