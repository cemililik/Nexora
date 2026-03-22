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

/// <summary>Command to generate a presigned URL for importing contacts via file upload.</summary>
public sealed record GenerateImportUploadUrlCommand(
    string FileName,
    string ContentType,
    long FileSize) : ICommand<ImportUploadUrlDto>;

/// <summary>Validates import upload URL generation input.</summary>
public sealed class GenerateImportUploadUrlValidator : AbstractValidator<GenerateImportUploadUrlCommand>
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedContentTypes =
    [
        "text/csv",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    ];

    public GenerateImportUploadUrlValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_filename_required")
            .MaximumLength(500).WithMessage("lockey_contacts_validation_import_filename_max_length")
            .Must(n => !n.Contains("..") && !n.Contains('/') && !n.Contains('\\'))
            .WithMessage("lockey_contacts_validation_import_filename_invalid_characters");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("lockey_contacts_validation_import_content_type_required")
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("lockey_contacts_validation_import_content_type_invalid");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("lockey_contacts_validation_import_file_empty")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("lockey_contacts_validation_import_file_too_large");
    }
}

/// <summary>Generates a presigned upload URL for contact import files.</summary>
public sealed class GenerateImportUploadUrlHandler(
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<GenerateImportUploadUrlHandler> logger) : ICommandHandler<GenerateImportUploadUrlCommand, ImportUploadUrlDto>
{
    /// <inheritdoc />
    public async Task<Result<ImportUploadUrlDto>> Handle(
        GenerateImportUploadUrlCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ImportUploadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ImportUploadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_contacts_error_invalid_organization_context"));

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";
        var storageKey = $"{orgId}/contacts/imports/{Guid.NewGuid()}/{request.FileName}";

        var result = await fileStorageService.GenerateUploadPresignedUrlAsync(
            bucketName,
            storageKey,
            request.ContentType,
            opts.DefaultPresignedUrlExpiry,
            cancellationToken);

        logger.LogInformation(
            "Generated import upload URL for file {FileName} in tenant {TenantId}, key {StorageKey}",
            request.FileName, tenantId, storageKey);

        var dto = new ImportUploadUrlDto(result.Url, storageKey, result.ExpiresAt);
        return Result<ImportUploadUrlDto>.Success(dto,
            LocalizedMessage.Of("lockey_contacts_import_upload_url_generated"));
    }
}
