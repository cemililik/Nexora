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

public sealed record GetReportExecutionByIdQuery(Guid Id) : IQuery<ReportExecutionDto>;

public sealed class GetReportExecutionByIdHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetReportExecutionByIdHandler> logger) : IQueryHandler<GetReportExecutionByIdQuery, ReportExecutionDto>
{
    public async Task<Result<ReportExecutionDto>> Handle(GetReportExecutionByIdQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var executionId = ReportExecutionId.From(request.Id);

        var execution = await dbContext.ReportExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.TenantId == tenantId, ct);

        if (execution is null)
        {
            logger.LogDebug("Report execution {ExecutionId} not found", request.Id);
            return Result<ReportExecutionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_execution_not_found"));
        }

        return Result<ReportExecutionDto>.Success(
            new ReportExecutionDto(execution.Id.Value, execution.DefinitionId.Value,
                execution.Status.ToString(), execution.ParameterValues, execution.Format.ToString(),
                execution.RowCount, execution.DurationMs, execution.ErrorDetails,
                execution.ExecutedBy, execution.CreatedAt));
    }
}
