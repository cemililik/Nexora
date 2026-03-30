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

/// <summary>Command to revoke access permission on a folder.</summary>
public sealed record RevokeFolderAccessCommand(Guid FolderId, Guid AccessId) : ICommand;

/// <summary>Validates revoke folder access input.</summary>
public sealed class RevokeFolderAccessValidator : AbstractValidator<RevokeFolderAccessCommand>
{
    public RevokeFolderAccessValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");
        RuleFor(x => x.AccessId)
            .NotEmpty().WithMessage("lockey_documents_validation_access_id_required");
    }
}

/// <summary>Revokes an access permission from a folder.</summary>
public sealed class RevokeFolderAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RevokeFolderAccessHandler> logger) : ICommandHandler<RevokeFolderAccessCommand>
{
    public async Task<Result> Handle(
        RevokeFolderAccessCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var folderId = FolderId.From(request.FolderId);

        var folder = await dbContext.Folders
            .Include(f => f.AccessList)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (folder is null)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        var accessId = FolderAccessId.From(request.AccessId);
        if (!folder.AccessList.Any(a => a.Id == accessId))
        {
            logger.LogWarning("Access {AccessId} not found on folder {FolderId} for tenant {TenantId}",
                request.AccessId, folder.Id, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_access_not_found"));
        }

        folder.RevokeAccess(accessId);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Access {AccessId} revoked on folder {FolderId} for tenant {TenantId}",
            request.AccessId, folder.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_documents_folder_access_revoked"));
    }
}
