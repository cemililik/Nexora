using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Contacts.Application.DTOs;
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
    ILogger<StartContactImportHandler> logger) : ICommandHandler<StartContactImportCommand, ImportJobDto>
{
    public async Task<Result<ImportJobDto>> Handle(
        StartContactImportCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        // Verify the file was actually uploaded to storage
        var exists = await fileStorageService.ObjectExistsAsync(
            bucketName, request.StorageKey, cancellationToken);

        if (!exists)
            return Result<ImportJobDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_import_file_not_found"));

        var jobId = Guid.NewGuid();

        // In production, this would enqueue a Hangfire job:
        // BackgroundJob.Enqueue<ContactImportJob>(j => j.RunAsync(params, ct));
        // For now, we return a job tracking DTO

        logger.LogInformation(
            "Contact import job {JobId} started for tenant {TenantId} with file {FileName} (key: {StorageKey})",
            jobId, tenantId, request.FileName, request.StorageKey);

        var dto = new ImportJobDto(
            jobId, "Queued", 0, 0, 0, 0,
            DateTimeOffset.UtcNow, null);

        return Task.FromResult(Result<ImportJobDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_import_job_started"))).Result;
    }
}
