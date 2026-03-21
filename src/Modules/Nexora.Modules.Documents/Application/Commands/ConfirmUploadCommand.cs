using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to confirm a completed file upload and create the document record.</summary>
public sealed record ConfirmUploadCommand(
    Guid FolderId,
    string StorageKey,
    string Name,
    string MimeType,
    long FileSize,
    string? Description = null,
    Guid? LinkedEntityId = null,
    string? LinkedEntityType = null,
    string? Tags = null) : ICommand<DocumentDto>;

/// <summary>Validates upload confirmation input.</summary>
public sealed class ConfirmUploadValidator : AbstractValidator<ConfirmUploadCommand>
{
    private const long MaxFileSize = 52_428_800; // 50 MB

    public ConfirmUploadValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");

        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_documents_validation_storage_key_required")
            .MaximumLength(1000).WithMessage("lockey_documents_validation_storage_key_max_length");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_documents_validation_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_name_max_length");

        RuleFor(x => x.MimeType)
            .NotEmpty().WithMessage("lockey_documents_validation_mime_type_required")
            .MaximumLength(100).WithMessage("lockey_documents_validation_mime_type_max_length");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("lockey_documents_validation_file_size_positive")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("lockey_documents_validation_file_size_max");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("lockey_documents_validation_description_max_length");

        RuleFor(x => x.LinkedEntityType)
            .MaximumLength(100).WithMessage("lockey_documents_validation_entity_type_max_length");

        RuleFor(x => x.Tags)
            .MaximumLength(2000).WithMessage("lockey_documents_validation_tags_max_length");
    }
}

/// <summary>Confirms a file upload by verifying the object in storage and creating a document record.</summary>
public sealed class ConfirmUploadHandler(
    DocumentsDbContext dbContext,
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<ConfirmUploadHandler> logger) : ICommandHandler<ConfirmUploadCommand, DocumentDto>
{
    /// <inheritdoc />
    public async Task<Result<DocumentDto>> Handle(
        ConfirmUploadCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
        {
            logger.LogWarning("UserId missing or invalid in tenant context for upload confirmation in tenant {TenantId}", tenantId);
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
        }

        // Verify folder exists within same organization
        var folderId = FolderId.From(request.FolderId);
        var folderExists = await dbContext.Folders
            .AnyAsync(f => f.Id == folderId && f.TenantId == tenantId && f.OrganizationId == orgId, cancellationToken);

        if (!folderExists)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        // Verify file exists in storage
        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var objectExists = await fileStorageService.ObjectExistsAsync(bucketName, request.StorageKey, cancellationToken);
        if (!objectExists)
        {
            logger.LogWarning("Object {StorageKey} not found in bucket {BucketName}", request.StorageKey, bucketName);
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_storage_object_not_found"));
        }

        // Create document record
        var document = Document.Create(
            tenantId, orgId, folderId, parsedUid,
            request.Name, request.MimeType, request.FileSize, request.StorageKey,
            request.Description, request.LinkedEntityId, request.LinkedEntityType, request.Tags);

        await dbContext.Documents.AddAsync(document, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Upload confirmed: Document {DocumentId} created in folder {FolderId} for tenant {TenantId}",
            document.Id, folderId, tenantId);

        var dto = new DocumentDto(
            document.Id.Value, document.FolderId.Value, document.Name, document.Description,
            document.MimeType, document.FileSize, document.StorageKey, document.Status.ToString(),
            document.LinkedEntityId, document.LinkedEntityType,
            document.CurrentVersion, document.CreatedAt);

        return Result<DocumentDto>.Success(dto, LocalizedMessage.Of("lockey_documents_upload_confirmed"));
    }
}
