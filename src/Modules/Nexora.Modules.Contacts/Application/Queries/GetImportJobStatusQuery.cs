using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.ValueObjects;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to check the status of a contact import job.</summary>
public sealed record GetImportJobStatusQuery(Guid JobId) : IQuery<ImportJobDto>;

/// <summary>Retrieves import job status by job ID.</summary>
public sealed class GetImportJobStatusHandler(
    ContactsDbContext dbContext,
    ILogger<GetImportJobStatusHandler> logger) : IQueryHandler<GetImportJobStatusQuery, ImportJobDto>
{
    public async Task<Result<ImportJobDto>> Handle(
        GetImportJobStatusQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Import job status requested for {JobId}", request.JobId);

        var importJobId = ImportJobId.From(request.JobId);
        var importJob = await dbContext.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == importJobId, cancellationToken);

        if (importJob is null)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_job_not_found"));

        var dto = new ImportJobDto(
            importJob.Id.Value,
            importJob.Status.ToString(),
            importJob.TotalRows,
            importJob.ProcessedRows,
            importJob.SuccessCount,
            importJob.ErrorCount,
            importJob.CreatedAt,
            importJob.CompletedAt);

        return Result<ImportJobDto>.Success(dto);
    }
}
