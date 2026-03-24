using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Reporting.Domain.ValueObjects;
using Nexora.Modules.Reporting.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Reporting.Application.Queries;

public sealed record DownloadReportResultQuery(Guid ExecutionId) : IQuery<DownloadReportResultDto>;

public sealed record DownloadReportResultDto(string Url, DateTimeOffset ExpiresAt);

public sealed class DownloadReportResultHandler(
    ReportingDbContext dbContext,
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<DownloadReportResultQuery, DownloadReportResultDto>
{
    public async Task<Result<DownloadReportResultDto>> Handle(DownloadReportResultQuery request, CancellationToken ct)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var executionId = ReportExecutionId.From(request.ExecutionId);

        var execution = await dbContext.ReportExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId && e.TenantId == tenantId, ct);

        if (execution is null)
            return Result<DownloadReportResultDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_execution_not_found"));

        if (execution.Status != ReportStatus.Completed || string.IsNullOrEmpty(execution.ResultStorageKey))
            return Result<DownloadReportResultDto>.Failure(
                LocalizedMessage.Of("lockey_reporting_error_result_not_available"));

        var result = await fileStorageService.GenerateDownloadPresignedUrlAsync(
            "nexora-reports", execution.ResultStorageKey, TimeSpan.FromHours(1), ct);

        return Result<DownloadReportResultDto>.Success(
            new DownloadReportResultDto(result.Url, result.ExpiresAt));
    }
}
