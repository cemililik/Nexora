using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>
/// Persists an <see cref="ExportJob"/>, enqueues <see cref="ContactExportJob"/> on the bulk queue,
/// and commits both atomically so the client always sees a tracked Hangfire job.
/// </summary>
public sealed class StartContactExportHandler(
    ITenantContextAccessor tenantContextAccessor,
    IBackgroundJobClient backgroundJobClient,
    ContactsDbContext dbContext,
    ILogger<StartContactExportHandler> logger) : ICommandHandler<StartContactExportCommand, ExportJobDto>
{
    /// <inheritdoc />
    public async Task<Result<ExportJobDto>> Handle(
        StartContactExportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ExportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ExportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));

        var format = request.Format.ToLowerInvariant();

        var filters = new ExportFiltersPayload(
            request.StatusFilter,
            request.TypeFilter,
            request.DateFrom,
            request.DateTo,
            request.DateField);

        var fields = new ExportFieldsPayload(
            request.Fields?.ToList(),
            request.CustomFieldIds?.ToList());

        var filtersJson = HasAnyFilter(filters) ? JsonSerializer.Serialize(filters) : null;
        var fieldsJson = HasAnyField(fields) ? JsonSerializer.Serialize(fields) : null;

        var userId = tenantContextAccessor.Current.UserId;
        var exportJob = ExportJob.Create(tenantId, orgId, format, filtersJson, fieldsJson, userId);

        Guid? triggeredByUserGuid = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : null;

        var hangfireJobId = backgroundJobClient.Enqueue<ContactExportJob>(j => j.RunAsync(
            new ContactExportJobParams
            {
                TenantId = tenantId.ToString(),
                OrganizationId = orgId.ToString(),
                OrganizationIdGuid = orgId,
                ExportJobId = exportJob.Id.Value,
                Format = format,
                Fields = request.Fields,
                CustomFieldIds = request.CustomFieldIds,
                StatusFilter = request.StatusFilter,
                TypeFilter = request.TypeFilter,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                DateField = request.DateField,
                TriggeredByUserId = triggeredByUserGuid
            },
            CancellationToken.None));

        exportJob.SetHangfireJobId(hangfireJobId);

        await dbContext.ExportJobs.AddAsync(exportJob, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Contact export job {JobId} (Hangfire: {HangfireJobId}) started for tenant {TenantId} in format {Format}",
            exportJob.Id, hangfireJobId, tenantId, format);

        var dto = new ExportJobDto(
            exportJob.Id.Value,
            exportJob.Status.ToString(),
            exportJob.Format,
            exportJob.TotalRows,
            exportJob.CreatedAt,
            exportJob.CompletedAt,
            DownloadUrl: null,
            ErrorDetails: null);

        return Result<ExportJobDto>.Success(
            dto, LocalizedMessage.Of("lockey_contacts_export_job_started"));
    }

    private static bool HasAnyFilter(ExportFiltersPayload f) =>
        !string.IsNullOrWhiteSpace(f.StatusFilter)
        || !string.IsNullOrWhiteSpace(f.TypeFilter)
        || f.DateFrom.HasValue
        || f.DateTo.HasValue
        || !string.IsNullOrWhiteSpace(f.DateField);

    private static bool HasAnyField(ExportFieldsPayload f) =>
        f.Fields is { Count: > 0 } || f.CustomFieldIds is { Count: > 0 };
}
