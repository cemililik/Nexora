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

public sealed record DeleteReportScheduleCommand(Guid Id) : ICommand;

/// <summary>Validates report schedule deletion input.</summary>
public sealed class DeleteReportScheduleValidator : AbstractValidator<DeleteReportScheduleCommand>
{
    public DeleteReportScheduleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class DeleteReportScheduleHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteReportScheduleHandler> logger) : ICommandHandler<DeleteReportScheduleCommand>
{
    public async Task<Result> Handle(DeleteReportScheduleCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var scheduleId = ReportScheduleId.From(request.Id);

        var schedule = await dbContext.ReportSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId, ct);

        if (schedule is null)
            return Result.Failure(LocalizedMessage.Of("lockey_reporting_error_schedule_not_found"));

        dbContext.ReportSchedules.Remove(schedule);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report schedule {ScheduleId} deleted for tenant {TenantId}",
            request.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_reporting_schedule_deleted"));
    }
}
