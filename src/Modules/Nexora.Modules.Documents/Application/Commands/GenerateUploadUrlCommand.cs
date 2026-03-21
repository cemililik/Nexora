using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to generate a presigned URL for direct file upload to storage.</summary>
public sealed record GenerateUploadUrlCommand(
    string FileName,
    string ContentType,
    long FileSize) : ICommand<UploadUrlDto>;

/// <summary>Validates upload URL generation input.</summary>
public sealed class GenerateUploadUrlValidator : AbstractValidator<GenerateUploadUrlCommand>
{
    private const long MaxFileSize = 52_428_800; // 50 MB

    public GenerateUploadUrlValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("lockey_documents_validation_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_name_max_length")
            .Must(n => !n.Contains("..") && !n.Contains('/') && !n.Contains('\\'))
            .WithMessage("lockey_documents_validation_name_invalid_characters");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("lockey_documents_validation_mime_type_required")
            .MaximumLength(100).WithMessage("lockey_documents_validation_mime_type_max_length");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("lockey_documents_validation_file_size_positive")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("lockey_documents_validation_file_size_max");
    }
}

/// <summary>Generates a presigned upload URL and assigns a storage key for the file.</summary>
public sealed class GenerateUploadUrlHandler(
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<GenerateUploadUrlHandler> logger) : ICommandHandler<GenerateUploadUrlCommand, UploadUrlDto>
{
    /// <inheritdoc />
    public async Task<Result<UploadUrlDto>> Handle(
        GenerateUploadUrlCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<UploadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<UploadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";
        var storageKey = $"{orgId}/documents/{Guid.NewGuid()}/{request.FileName}";

        var result = await fileStorageService.GenerateUploadPresignedUrlAsync(
            bucketName,
            storageKey,
            request.ContentType,
            opts.DefaultPresignedUrlExpiry,
            cancellationToken);

        logger.LogInformation(
            "Generated upload URL for file {FileName} in tenant {TenantId}, key {StorageKey}",
            request.FileName, tenantId, storageKey);

        var dto = new UploadUrlDto(result.Url, storageKey, result.ExpiresAt);
        return Result<UploadUrlDto>.Success(dto, LocalizedMessage.Of("lockey_documents_upload_url_generated"));
    }
}
