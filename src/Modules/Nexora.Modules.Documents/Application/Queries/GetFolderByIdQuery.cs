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

/// <summary>Query to get a folder by ID.</summary>
public sealed record GetFolderByIdQuery(Guid FolderId) : IQuery<FolderDto>;

/// <summary>Returns a single folder by its ID.</summary>
public sealed class GetFolderByIdHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetFolderByIdHandler> logger) : IQueryHandler<GetFolderByIdQuery, FolderDto>
{
    public async Task<Result<FolderDto>> Handle(
        GetFolderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var folderId = FolderId.From(request.FolderId);

        var folder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId && f.TenantId == tenantId, cancellationToken);

        if (folder is null)
        {
            logger.LogDebug("Folder {FolderId} not found", request.FolderId);
            return Result<FolderDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        var dto = new FolderDto(
            folder.Id.Value, folder.Name, folder.Path,
            folder.ParentFolderId?.Value, folder.ModuleName,
            folder.IsSystem, folder.CreatedAt);

        return Result<FolderDto>.Success(dto,
            LocalizedMessage.Of("lockey_documents_folder_retrieved"));
    }
}
