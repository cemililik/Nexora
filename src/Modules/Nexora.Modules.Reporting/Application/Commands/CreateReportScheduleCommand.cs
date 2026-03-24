using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record CreateReportScheduleCommand(
    Guid DefinitionId,
    string CronExpression,
    string Format,
    string? Recipients) : ICommand<ReportScheduleDto>;

public sealed class CreateReportScheduleValidator : AbstractValidator<CreateReportScheduleCommand>
{
    public CreateReportScheduleValidator()
    {
        RuleFor(x => x.DefinitionId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.CronExpression).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Format).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class CreateReportScheduleHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateReportScheduleHandler> logger) : ICommandHandler<CreateReportScheduleCommand, ReportScheduleDto>
{
    public async Task<Result<ReportScheduleDto>> Handle(CreateReportScheduleCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var definitionId = ReportDefinitionId.From(request.DefinitionId);

        var definitionExists = await dbContext.ReportDefinitions
            .AnyAsync(d => d.Id == definitionId && d.TenantId == tenantId, ct);

        if (!definitionExists)
            return Result<ReportScheduleDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));

        if (!Enum.TryParse<ReportFormat>(request.Format, true, out var format))
            return Result<ReportScheduleDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_invalid_format"));

        var schedule = ReportSchedule.Create(
            tenantId, definitionId, request.CronExpression, format, request.Recipients);

        await dbContext.ReportSchedules.AddAsync(schedule, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Report schedule {ScheduleId} created for definition {DefinitionId} in tenant {TenantId}",
            schedule.Id, definitionId, tenantId);

        return Result<ReportScheduleDto>.Success(
            new ReportScheduleDto(schedule.Id.Value, schedule.DefinitionId.Value,
                schedule.CronExpression, schedule.Format.ToString(), schedule.Recipients,
                schedule.IsActive, schedule.LastExecutionAt, schedule.NextExecutionAt, schedule.CreatedAt),
            LocalizedMessage.Of("lockey_reporting_schedule_created"));
    }
}
