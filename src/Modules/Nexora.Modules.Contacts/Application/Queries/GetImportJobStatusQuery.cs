using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Queries;

/// <summary>Query to check the status of a contact import job.</summary>
public sealed record GetImportJobStatusQuery(Guid JobId) : IQuery<ImportJobDto>;

/// <summary>Retrieves import job status by job ID.</summary>
public sealed class GetImportJobStatusHandler(
    ILogger<GetImportJobStatusHandler> logger) : IQueryHandler<GetImportJobStatusQuery, ImportJobDto>
{
    public Task<Result<ImportJobDto>> Handle(
        GetImportJobStatusQuery request,
        CancellationToken cancellationToken)
    {
        // In production, this would query Hangfire job storage or a dedicated jobs table.
        // For now, return a placeholder indicating the job is not found or queued.
        logger.LogDebug("Import job status requested for {JobId}", request.JobId);

        // Placeholder: In production, look up from Hangfire or a job tracking table
        return Task.FromResult(
            Result<ImportJobDto>.Failure(LocalizedMessage.Of("lockey_contacts_error_import_job_not_found")));
    }
}
