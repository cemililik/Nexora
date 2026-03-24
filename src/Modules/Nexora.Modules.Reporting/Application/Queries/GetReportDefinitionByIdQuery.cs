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

public sealed record GetReportDefinitionByIdQuery(Guid Id) : IQuery<ReportDefinitionDto>;

public sealed class GetReportDefinitionByIdHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetReportDefinitionByIdHandler> logger) : IQueryHandler<GetReportDefinitionByIdQuery, ReportDefinitionDto>
{
    public async Task<Result<ReportDefinitionDto>> Handle(GetReportDefinitionByIdQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var definitionId = ReportDefinitionId.From(request.Id);

        var definition = await dbContext.ReportDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.TenantId == tenantId, ct);

        if (definition is null)
        {
            logger.LogDebug("Report definition {DefinitionId} not found", request.Id);
            return Result<ReportDefinitionDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));
        }

        return Result<ReportDefinitionDto>.Success(
            new ReportDefinitionDto(definition.Id.Value, definition.Name, definition.Description,
                definition.Module, definition.Category, definition.QueryText, definition.Parameters,
                definition.DefaultFormat.ToString(), definition.IsActive, definition.CreatedAt, definition.CreatedBy));
    }
}
