using FluentValidation;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.DTOs;
using Nexora.Modules.Contacts.Domain.Entities;
using Nexora.Modules.Contacts.Infrastructure;
using Nexora.Modules.Contacts.Infrastructure.Jobs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Contacts.Application.Commands;

/// <summary>Command to start a contact import job from a previously uploaded file.</summary>
public sealed record StartContactImportCommand(
    string FileName,
    string FileFormat,
    string StorageKey) : ICommand<ImportJobDto>;

/// <summary>Validates contact import input.</summary>
public sealed class StartContactImportValidator : AbstractValidator<StartContactImportCommand>
{
    private static readonly string[] ValidFormats = ["csv", "xlsx"];

    public StartContactImportValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_filename_required");

        RuleFor(x => x.FileFormat)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_format_required")
            .Must(f => ValidFormats.Contains(f.ToLowerInvariant()))
            .WithMessage("lockey_contacts_validation_import_format_invalid");

        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_storage_key_required");
    }
}

/// <summary>Verifies the uploaded file exists in storage and starts a background import job.</summary>
public sealed class StartContactImportHandler(
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    IBackgroundJobClient backgroundJobClient,
    ContactsDbContext dbContext,
    ILogger<StartContactImportHandler> logger) : ICommandHandler<StartContactImportCommand, ImportJobDto>
{
    public async Task<Result<ImportJobDto>> Handle(
        StartContactImportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));

        var expectedPrefix = $"{orgId}/contacts/imports/";
        if (!request.StorageKey.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Storage key {StorageKey} does not match expected prefix for organization {OrganizationId} in tenant {TenantId}",
                request.StorageKey, orgId, tenantId);

            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_invalid_storage_key"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        // Verify the file was actually uploaded to storage
        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);

        if (!exists)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));

        var userId = tenantContextAccessor.Current.UserId;
        var importJob = ImportJob.Create(
            tenantId, orgId, request.FileName, request.FileFormat, request.StorageKey, userId);

        await dbContext.ImportJobs.AddAsync(importJob, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var hangfireJobId = backgroundJobClient.Enqueue<ContactImportJob>(j => j.RunAsync(
            new ContactImportJobParams
            {
                TenantId = tenantId.ToString(),
                OrganizationId = orgId.ToString(),
                OrganizationIdGuid = orgId,
                FileName = request.FileName,
                FileFormat = request.FileFormat,
                StorageKey = request.StorageKey,
                ImportJobId = importJob.Id.Value,
            },
            CancellationToken.None));

        importJob.SetHangfireJobId(hangfireJobId);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Contact import job {JobId} (Hangfire: {HangfireJobId}) started for tenant {TenantId} with file {FileName} (key: {StorageKey})",
            importJob.Id, hangfireJobId, tenantId, request.FileName, request.StorageKey);

        var dto = new ImportJobDto(
            importJob.Id.Value, importJob.Status.ToString(), 0, 0, 0, 0,
            importJob.CreatedAt, null);

        return Result<ImportJobDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_import_job_started"));
    }
}
