using System.Text.Json;
using FluentValidation;
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
/// Command to start a contact export job. Persists an <see cref="ExportJob"/>,
/// enqueues a Hangfire background job, and returns the client-facing DTO.
/// </summary>
public sealed record StartContactExportCommand(
    string Format,
    IReadOnlyList<string>? Fields = null,
    IReadOnlyList<Guid>? CustomFieldIds = null,
    string? StatusFilter = null,
    string? TypeFilter = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? DateField = null) : ICommand<ExportJobDto>;

/// <summary>Validates contact export input.</summary>
public sealed class StartContactExportValidator : AbstractValidator<StartContactExportCommand>
{
    /// <summary>Allowed output formats for contact export.</summary>
    public static readonly IReadOnlySet<string> ValidFormats =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "csv", "xlsx", "vcard" };

    /// <summary>Date fields that may be used as a range filter.</summary>
    public static readonly IReadOnlySet<string> ValidDateFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CreatedAt", "UpdatedAt" };

    /// <summary>Core contact fields the user may include in the export.</summary>
    public static readonly IReadOnlySet<string> AllowedCoreFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "firstName", "lastName", "displayName",
            "email", "phone", "mobile", "website",
            "companyName", "taxId", "title",
            "type", "status", "source",
            "language", "currency",
            "createdAt", "updatedAt"
        };

    /// <summary>Initialises validation rules.</summary>
    public StartContactExportValidator()
    {
        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_contacts_validation_export_format_required")
            .Must(f => !string.IsNullOrWhiteSpace(f) && ValidFormats.Contains(f))
            .WithMessage("lockey_contacts_validation_export_format_invalid");

        RuleFor(x => x.DateField!)
            .Must(f => ValidDateFields.Contains(f))
            .When(x => !string.IsNullOrWhiteSpace(x.DateField))
            .WithMessage("lockey_contacts_validation_export_date_field_invalid");

        RuleFor(x => x)
            .Must(x => !(x.DateFrom.HasValue && x.DateTo.HasValue) || x.DateFrom <= x.DateTo)
            .WithMessage("lockey_contacts_validation_export_date_range_invalid");

        RuleFor(x => x.Fields!)
            .Must(fields => fields.All(f => AllowedCoreFields.Contains(f)))
            .When(x => x.Fields is { Count: > 0 })
            .WithMessage("lockey_contacts_validation_export_fields_invalid");
    }
}

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

/// <summary>Serialization shape for <see cref="ExportJob.FiltersJson"/>.</summary>
public sealed record ExportFiltersPayload(
    string? StatusFilter,
    string? TypeFilter,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? DateField);

/// <summary>Serialization shape for <see cref="ExportJob.FieldsJson"/>.</summary>
public sealed record ExportFieldsPayload(
    IReadOnlyList<string>? Fields,
    IReadOnlyList<Guid>? CustomFieldIds);
