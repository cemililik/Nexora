using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Commands;

public sealed record DeleteReportDefinitionCommand(Guid Id) : ICommand;

public sealed class DeleteReportDefinitionHandler(
    ReportingDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteReportDefinitionHandler> logger) : ICommandHandler<DeleteReportDefinitionCommand>
{
    public async Task<Result> Handle(DeleteReportDefinitionCommand request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var definitionId = ReportDefinitionId.From(request.Id);

        var definition = await dbContext.ReportDefinitions
            .FirstOrDefaultAsync(d => d.Id == definitionId && d.TenantId == tenantId, ct);

        if (definition is null)
            return Result.Failure(LocalizedMessage.Of("lockey_reporting_error_definition_not_found"));

        dbContext.ReportDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Report definition {DefinitionId} deleted for tenant {TenantId}",
            request.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_reporting_definition_deleted"));
    }
}
