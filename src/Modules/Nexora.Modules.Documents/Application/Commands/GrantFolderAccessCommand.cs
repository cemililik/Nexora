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

/// <summary>Command to grant access permission on a folder.</summary>
public sealed record GrantFolderAccessCommand(
    Guid FolderId, Guid? UserId, Guid? RoleId, string Permission, DateTime? ExpiresAt) : ICommand<FolderAccessDto>;

/// <summary>Validates grant folder access input.</summary>
public sealed class GrantFolderAccessValidator : AbstractValidator<GrantFolderAccessCommand>
{
    public GrantFolderAccessValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");

        RuleFor(x => x.Permission)
            .NotEmpty().WithMessage("lockey_documents_validation_permission_required")
            .Must(p => Enum.TryParse<AccessPermission>(p, ignoreCase: true, out _))
            .WithMessage("lockey_documents_validation_permission_invalid");

        RuleFor(x => x)
            .Must(x =>
            {
                var hasUser = x.UserId.HasValue && x.UserId.Value != Guid.Empty;
                var hasRole = x.RoleId.HasValue && x.RoleId.Value != Guid.Empty;
                return hasUser ^ hasRole;
            })
            .WithMessage("lockey_documents_validation_exactly_one_principal_required");
    }
}

/// <summary>Grants access permission on a folder to a user or role.</summary>
public sealed class GrantFolderAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GrantFolderAccessHandler> logger) : ICommandHandler<GrantFolderAccessCommand, FolderAccessDto>
{
    public async Task<Result<FolderAccessDto>> Handle(
        GrantFolderAccessCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<FolderAccessDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var folderId = FolderId.From(request.FolderId);

        var folder = await dbContext.Folders
            .Include(f => f.AccessList)
            .FirstOrDefaultAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (folder is null)
        {
            logger.LogWarning("Folder {FolderId} not found for tenant {TenantId}", request.FolderId, tenantId);
            return Result<FolderAccessDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        var permission = Enum.Parse<AccessPermission>(request.Permission, ignoreCase: true);
        var access = folder.GrantAccess(request.UserId, request.RoleId, permission, request.ExpiresAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Access granted on folder {FolderId} for tenant {TenantId}", folder.Id, tenantId);

        var dto = new FolderAccessDto(
            access.Id.Value, access.UserId, access.RoleId, access.Permission.ToString(),
            access.ExpiresAt, access.IsExpired());

        return Result<FolderAccessDto>.Success(dto, LocalizedMessage.Of("lockey_documents_folder_access_granted"));
    }
}
