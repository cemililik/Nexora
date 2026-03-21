using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to create a new folder.</summary>
public sealed record CreateFolderCommand(
    string Name,
    Guid? ParentFolderId = null,
    string? ModuleName = null,
    Guid? ModuleRef = null,
    bool IsSystem = false) : ICommand<FolderDto>;

/// <summary>Validates folder creation input.</summary>
public sealed class CreateFolderValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_name_required")
            .MaximumLength(255).WithMessage("lockey_documents_validation_folder_name_max_length")
            .Matches(@"^[^/\\\p{Cc}]+$").WithMessage("lockey_documents_validation_folder_name_invalid_characters");

        RuleFor(x => x.ModuleName)
            .MaximumLength(50).WithMessage("lockey_documents_validation_module_name_max_length");
    }
}

/// <summary>Creates a folder and persists it to the database.</summary>
public sealed class CreateFolderHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateFolderHandler> logger) : ICommandHandler<CreateFolderCommand, FolderDto>
{
    public async Task<Result<FolderDto>> Handle(
        CreateFolderCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        string? parentPath = null;
        FolderId? parentFolderId = null;

        if (request.ParentFolderId.HasValue)
        {
            var parentId = FolderId.From(request.ParentFolderId.Value);
            var parent = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == parentId && f.TenantId == tenantId, cancellationToken);

            if (parent is null)
            {
                logger.LogWarning("Parent folder {ParentFolderId} not found for tenant {TenantId}",
                    request.ParentFolderId, tenantId);
                return Result<FolderDto>.Failure(
                    LocalizedMessage.Of("lockey_documents_error_parent_folder_not_found"));
            }

            parentPath = parent.Path;
            parentFolderId = parentId;
        }

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
        {
            logger.LogWarning("UserId missing or invalid in tenant context for folder creation in tenant {TenantId}", tenantId);
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
        }

        var folder = Folder.Create(
            tenantId, orgId, request.Name, parsedUid,
            parentPath, parentFolderId,
            request.ModuleName, request.ModuleRef, request.IsSystem);

        await dbContext.Folders.AddAsync(folder, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Folder {FolderId} created for tenant {TenantId}", folder.Id, tenantId);

        var dto = new FolderDto(
            folder.Id.Value, folder.Name, folder.Path,
            folder.ParentFolderId?.Value, folder.ModuleName,
            folder.IsSystem, folder.CreatedAt);

        return Result<FolderDto>.Success(dto, LocalizedMessage.Of("lockey_documents_folder_created"));
    }
}
