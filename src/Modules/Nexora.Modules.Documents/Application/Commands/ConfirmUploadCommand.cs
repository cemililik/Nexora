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
    string? Tags = null) : ICommand<ConfirmUploadResultDto>;

/// <summary>Validates upload confirmation input.</summary>
public sealed class ConfirmUploadValidator : AbstractValidator<ConfirmUploadCommand>
{
    private const long MaxFileSize = 104_857_600; // 100 MB

    /// <summary>Allowed MIME types for upload.</summary>
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain",
        "text/csv",
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/svg+xml",
    };

    public ConfirmUploadValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");

        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_documents_validation_storage_key_required")
            .MaximumLength(1000).WithMessage("lockey_documents_validation_storage_key_max_length");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_documents_validation_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_name_max_length")
            .Must(name => !ContainsPathTraversal(name))
            .WithMessage("lockey_documents_validation_name_path_traversal");

        RuleFor(x => x.MimeType)
            .NotEmpty().WithMessage("lockey_documents_validation_mime_type_required")
            .MaximumLength(100).WithMessage("lockey_documents_validation_mime_type_max_length")
            .Must(mime => AllowedMimeTypes.Contains(mime))
            .WithMessage("lockey_documents_validation_mime_type_not_allowed");

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

    /// <summary>Checks for path traversal patterns in file names.</summary>
    private static bool ContainsPathTraversal(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.Contains("..") || name.Contains('/') || name.Contains('\\');
    }
}

/// <summary>Confirms a file upload by verifying the object in storage and creating a document record.</summary>
public sealed class ConfirmUploadHandler(
    DocumentsDbContext dbContext,
    IFileStorageService fileStorageService,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<ConfirmUploadHandler> logger) : ICommandHandler<ConfirmUploadCommand, ConfirmUploadResultDto>
{
    /// <inheritdoc />
    public async Task<Result<ConfirmUploadResultDto>> Handle(
        ConfirmUploadCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<ConfirmUploadResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<ConfirmUploadResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
        {
            logger.LogWarning("UserId missing or invalid in tenant context for upload confirmation in tenant {TenantId}", tenantId);
            return Result<ConfirmUploadResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
        }

        // Verify folder exists within same organization
        var folderId = FolderId.From(request.FolderId);
        var folderExists = await dbContext.Folders
            .AnyAsync(f => f.Id == folderId && f.TenantId == tenantId && f.OrganizationId == orgId, cancellationToken);

        if (!folderExists)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result<ConfirmUploadResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        // Verify file exists in storage
        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var objectExists = await fileStorageService.ObjectExistsAsync(bucketName, request.StorageKey, cancellationToken);
        if (!objectExists)
        {
            logger.LogWarning("Object {StorageKey} not found in bucket {BucketName}", request.StorageKey, bucketName);
            return Result<ConfirmUploadResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_storage_object_not_found"));
        }

        // Check for duplicate: same Name in same folder for same tenant
        var existingDoc = await dbContext.Documents
            .FirstOrDefaultAsync(
                d => d.Name == request.Name.Trim()
                     && d.FolderId == folderId
                     && d.TenantId == tenantId
                     && d.OrganizationId == orgId
                     && !d.IsDeleted,
                cancellationToken);

        if (existingDoc is not null)
        {
            // Add as new version instead of creating a duplicate
            var version = existingDoc.AddVersion(request.StorageKey, request.FileSize, parsedUid);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Duplicate upload detected: added version {VersionNumber} to Document {DocumentId} in folder {FolderId} for tenant {TenantId}",
                existingDoc.CurrentVersion, existingDoc.Id, folderId, tenantId);

            var existingDto = new DocumentDto(
                existingDoc.Id.Value, existingDoc.FolderId.Value, existingDoc.Name, existingDoc.Description,
                existingDoc.MimeType, existingDoc.FileSize, existingDoc.StorageKey, existingDoc.Status.ToString(),
                existingDoc.LinkedEntityId, existingDoc.LinkedEntityType,
                existingDoc.CurrentVersion, existingDoc.CreatedAt);

            return Result<ConfirmUploadResultDto>.Success(
                new ConfirmUploadResultDto(existingDto, IsVersionUpdate: true),
                LocalizedMessage.Of("lockey_documents_toast_version_added"));
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

        return Result<ConfirmUploadResultDto>.Success(
            new ConfirmUploadResultDto(dto, IsVersionUpdate: false),
            LocalizedMessage.Of("lockey_documents_upload_confirmed"));
    }
}
