using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to rename an existing folder.</summary>
public sealed record RenameFolderCommand(Guid FolderId, string NewName) : ICommand<FolderDto>;

/// <summary>Validates folder rename input.</summary>
public sealed class RenameFolderValidator : AbstractValidator<RenameFolderCommand>
{
    public RenameFolderValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_name_required")
            .MaximumLength(255).WithMessage("lockey_documents_validation_folder_name_max_length");
    }
}

/// <summary>Renames a folder and cascades path updates to children.</summary>
public sealed class RenameFolderHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RenameFolderHandler> logger) : ICommandHandler<RenameFolderCommand, FolderDto>
{
    public async Task<Result<FolderDto>> Handle(
        RenameFolderCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var folderId = FolderId.From(request.FolderId);

        var folder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (folder is null)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        var oldPath = folder.Path;
        folder.Rename(request.NewName);

        // Compute new path
        var parentPath = oldPath[..oldPath.LastIndexOf('/')];
        var newPath = string.IsNullOrEmpty(parentPath)
            ? $"/{folder.Name}"
            : $"{parentPath}/{folder.Name}";
        folder.UpdatePath(newPath);

        // Cascade path updates to descendant folders
        var descendants = await dbContext.Folders
            .Where(f => f.TenantId == tenantId && f.Path.StartsWith(oldPath + "/"))
            .ToListAsync(cancellationToken);

        foreach (var descendant in descendants)
            descendant.UpdatePath(string.Concat(newPath, descendant.Path.AsSpan(oldPath.Length)));

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Folder {FolderId} renamed to {NewName} for tenant {TenantId}",
            folder.Id, request.NewName, tenantId);

        var dto = new FolderDto(
            folder.Id.Value, folder.Name, folder.Path,
            folder.ParentFolderId?.Value, folder.ModuleName,
            folder.IsSystem, folder.CreatedAt);

        return Result<FolderDto>.Success(dto, LocalizedMessage.Of("lockey_documents_folder_renamed"));
    }
}
