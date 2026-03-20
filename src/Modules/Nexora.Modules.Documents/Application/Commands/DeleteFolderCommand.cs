using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to delete a folder.</summary>
public sealed record DeleteFolderCommand(Guid FolderId) : ICommand;

/// <summary>Validates folder deletion input.</summary>
public sealed class DeleteFolderValidator : AbstractValidator<DeleteFolderCommand>
{
    public DeleteFolderValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");
    }
}

/// <summary>Deletes a folder if it is empty and not a system folder.</summary>
public sealed class DeleteFolderHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteFolderHandler> logger) : ICommandHandler<DeleteFolderCommand>
{
    public async Task<Result> Handle(
        DeleteFolderCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var folderId = FolderId.From(request.FolderId);

        var folder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (folder is null)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        if (folder.IsSystem)
        {
            logger.LogWarning("Cannot delete system folder {FolderId}", request.FolderId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_cannot_delete_system_folder"));
        }

        dbContext.Folders.Remove(folder);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("Folder {FolderId} is not empty, cannot delete", request.FolderId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_folder_not_empty"));
        }

        logger.LogInformation("Folder {FolderId} deleted for tenant {TenantId}", folder.Id, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_folder_deleted"));
    }
}
