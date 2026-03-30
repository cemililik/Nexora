using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Queries;

/// <summary>Query to get access permissions for a folder.</summary>
public sealed record GetFolderAccessQuery(Guid FolderId) : IQuery<IReadOnlyList<FolderAccessDto>>;

/// <summary>Returns all access permissions for a folder.</summary>
public sealed class GetFolderAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetFolderAccessHandler> logger) : IQueryHandler<GetFolderAccessQuery, IReadOnlyList<FolderAccessDto>>
{
    public async Task<Result<IReadOnlyList<FolderAccessDto>>> Handle(
        GetFolderAccessQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<IReadOnlyList<FolderAccessDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var folderId = FolderId.From(request.FolderId);

        var folderExists = await dbContext.Folders
            .AnyAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (!folderExists)
        {
            logger.LogDebug("Folder {FolderId} not found", request.FolderId);
            return Result<IReadOnlyList<FolderAccessDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        var accessList = await dbContext.FolderAccesses
            .AsNoTracking()
            .Where(a => a.FolderId == folderId)
            .OrderBy(a => a.Id)
            .Select(a => new FolderAccessDto(
                a.Id.Value, a.UserId, a.RoleId, a.Permission.ToString(),
                a.ExpiresAt, a.ExpiresAt.HasValue && a.ExpiresAt.Value <= DateTimeOffset.UtcNow))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<FolderAccessDto>>.Success(accessList,
            LocalizedMessage.Of("lockey_documents_folder_access_listed"));
    }
}
