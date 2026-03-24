using FluentValidation;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Application.DTOs;
using Nexora.Modules.Reporting.Domain.Entities;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record ExecuteReportCommand(
    Guid DefinitionId,
    string? Format,
    string? ParameterValues) : ICommand<ReportExecutionDto>;

public sealed class ExecuteReportValidator : AbstractValidator<ExecuteReportCommand>
{
    public ExecuteReportValidator()
    {
        RuleFor(x => x.DefinitionId).NotEmpty().WithMessage("lockey_validation_required");
    }
}

public sealed class ExecuteReportHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IBackgroundJobClient backgroundJobClient,
    ILogger<ExecuteReportHandler> logger) : ICommandHandler<ExecuteReportCommand, ReportExecutionDto>
{
    public async Task<Result<ReportExecutionDto>> Handle(ExecuteReportCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var userId = tenantContextAccessor.Current.UserId;
        var definitionId = ReportDefinitionId.From(request.DefinitionId);

        var definition = await dbContext.ReportDefinitions
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.TenantId == tenantId && d.IsActive, ct);

        if (definition is null)
            return Result<ReportExecutionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));

        var format = definition.DefaultFormat;
        if (!string.IsNullOrEmpty(request.Format) && Enum.TryParse<ReportFormat>(request.Format, true, out var f))
            format = f;

        var execution = ReportExecution.Create(
            tenantId, definitionId, format, request.ParameterValues, userId);

        await dbContext.ReportExecutions.AddAsync(execution, ct);
        await dbContext.SaveChangesAsync(ct);

        var hangfireJobId = backgroundJobClient.Enqueue<ReportExecutionJob>(j => j.RunAsync(
            new ReportExecutionJobParams
            {
                TenantId = tenantId.ToString(),
                OrganizationId = tenantContextAccessor.Current.OrganizationId,
                ExecutionId = execution.Id.Value,
            },
            CancellationToken.None));

        logger.LogInformation(
            "Report execution {ExecutionId} queued for definition {DefinitionId} in tenant {TenantId}, Hangfire job {JobId}",
            execution.Id, definitionId, tenantId, hangfireJobId);

        return Result<ReportExecutionDto>.Success(
            new ReportExecutionDto(execution.Id.Value, execution.DefinitionId.Value,
                execution.Status.ToString(), execution.ParameterValues, execution.Format.ToString(),
                null, null, null, execution.ExecutedBy, execution.CreatedAt),
            LocalizedMessage.Of("lockey_reporting_execution_queued"));
    }
}
