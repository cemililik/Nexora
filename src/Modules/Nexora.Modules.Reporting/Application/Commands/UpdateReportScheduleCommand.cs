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

public sealed record UpdateReportScheduleCommand(
    Guid Id,
    string CronExpression,
    string Format,
    string? Recipients) : ICommand<ReportScheduleDto>;

public sealed class UpdateReportScheduleValidator : AbstractValidator<UpdateReportScheduleCommand>
{
    public UpdateReportScheduleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.CronExpression).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Format).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class UpdateReportScheduleHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateReportScheduleHandler> logger) : ICommandHandler<UpdateReportScheduleCommand, ReportScheduleDto>
{
    public async Task<Result<ReportScheduleDto>> Handle(UpdateReportScheduleCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var scheduleId = ReportScheduleId.From(request.Id);

        var schedule = await dbContext.ReportSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.TenantId == tenantId, ct);

        if (schedule is null)
            return Result<ReportScheduleDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_schedule_not_found"));

        if (!Enum.TryParse<ReportFormat>(request.Format, true, out var format))
            return Result<ReportScheduleDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_format"));

        schedule.Update(request.CronExpression, format, request.Recipients);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report schedule {ScheduleId} updated for tenant {TenantId}",
            schedule.Id, tenantId);

        return Result<ReportScheduleDto>.Success(
            new ReportScheduleDto(schedule.Id.Value, schedule.DefinitionId.Value,
                schedule.CronExpression, schedule.Format.ToString(), schedule.Recipients,
                schedule.IsActive, schedule.LastExecutionAt, schedule.NextExecutionAt, schedule.CreatedAt),
            LocalizedMessage.Of("lockey_reporting_schedule_updated"));
    }
}
