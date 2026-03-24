using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record CreateDashboardCommand(
    string Name,
    string? Description,
    bool IsDefault) : ICommand<DashboardDto>;

public sealed class CreateDashboardValidator : AbstractValidator<CreateDashboardCommand>
{
    public CreateDashboardValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("lockey_validation_required")
            .MaximumLength(200).WithMessage("lockey_validation_max_length");
    }
}

public sealed class CreateDashboardHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateDashboardHandler> logger) : ICommandHandler<CreateDashboardCommand, DashboardDto>
{
    public async Task<Result<DashboardDto>> Handle(CreateDashboardCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = Guid.Parse(tenantContextAccessor.Current.OrganizationId!);

        var dashboard = Dashboard.Create(
            tenantId, orgId, request.Name, request.Description, request.IsDefault);

        await dbContext.Dashboards.AddAsync(dashboard, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Dashboard {DashboardId} created for tenant {TenantId}",
            dashboard.Id, tenantId);

        return Result<DashboardDto>.Success(
            new DashboardDto(dashboard.Id.Value, dashboard.Name, dashboard.Description,
                dashboard.IsDefault, dashboard.Widgets, dashboard.CreatedAt, dashboard.CreatedBy),
            LocalizedMessage.Of("lockey_reporting_dashboard_created"));
    }
}
