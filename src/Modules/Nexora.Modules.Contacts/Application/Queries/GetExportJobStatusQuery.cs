using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to check the status of a contact export job, including a presigned download URL when ready.</summary>
public sealed record GetExportJobStatusQuery(Guid JobId) : IQuery<ExportJobDto>;

/// <summary>
/// Retrieves export job status by job ID and, when completed, generates a 15-minute
/// presigned download URL for the stored file.
/// </summary>
public sealed class GetExportJobStatusHandler(
    ContactsDbContext dbContext,
    IFileStorageService fileStorageService,
    IOptions<StorageOptions> storageOptions,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetExportJobStatusHandler> logger) : IQueryHandler<GetExportJobStatusQuery, ExportJobDto>
{
    private static readonly TimeSpan DownloadUrlLifetime = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<Result<ExportJobDto>> Handle(
        GetExportJobStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ExportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        logger.LogDebug("Export job status requested for {JobId}", request.JobId);

        var exportJobId = ExportJobId.From(request.JobId);
        var exportJob = await dbContext.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                j => j.Id == exportJobId && j.TenantId == tenantId,
                cancellationToken);

        if (exportJob is null)
            return Result<ExportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_export_job_not_found"));

        string? downloadUrl = null;
        if (exportJob.Status == ExportJobStatus.Completed && !string.IsNullOrWhiteSpace(exportJob.StorageKey))
        {
            var opts = storageOptions.Value;
            var bucketName = $"{opts.BucketPrefix}-{tenantId}";
            var presigned = await fileStorageService.GenerateDownloadPresignedUrlAsync(
                bucketName, exportJob.StorageKey, DownloadUrlLifetime, cancellationToken);
            downloadUrl = presigned.Url;
        }

        var dto = new ExportJobDto(
            exportJob.Id.Value,
            exportJob.Status.ToString(),
            exportJob.Format,
            exportJob.TotalRows,
            exportJob.CreatedAt,
            exportJob.CompletedAt,
            downloadUrl,
            exportJob.ErrorDetails);

        return Result<ExportJobDto>.Success(dto);
    }
}
