using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.Modules.Reporting.Infrastructure.Services;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record GetReportFileQuery(Guid ExecutionId) : IQuery<ReportFileDto>;

public sealed record ReportFileDto(byte[] Data, string ContentType, string FileName);

public sealed class GetReportFileHandler(
    ReportingDbContext dbContext,
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetReportFileQuery, ReportFileDto>
{
    public async Task<Result<ReportFileDto>> Handle(GetReportFileQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var executionId = ReportExecutionId.From(request.ExecutionId);

        var execution = await dbContext.ReportExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.TenantId == tenantId, ct);

        if (execution is null)
            return Result<ReportFileDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_execution_not_found"));

        if (execution.Status != ReportStatus.Completed || string.IsNullOrEmpty(execution.ResultStorageKey))
            return Result<ReportFileDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_result_not_available"));

        var data = await fileStorageService.GetObjectAsync("nexora-reports", execution.ResultStorageKey, ct);
        var formatStr = execution.Format.ToString();
        var contentType = ReportExportService.GetContentType(formatStr);
        var extension = ReportExportService.GetFileExtension(formatStr);
        var fileName = $"report-{execution.Id.Value}{extension}";

        return Result<ReportFileDto>.Success(new ReportFileDto(data, contentType, fileName));
    }
}
