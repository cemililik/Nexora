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

/// <summary>Query to list folders, optionally filtered by parent or module.</summary>
public sealed record GetFoldersQuery(
    Guid? ParentFolderId = null, string? ModuleName = null) : IQuery<IReadOnlyList<FolderDto>>;

/// <summary>Returns a list of folders for the current tenant.</summary>
public sealed class GetFoldersHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetFoldersHandler> logger) : IQueryHandler<GetFoldersQuery, IReadOnlyList<FolderDto>>
{
    public async Task<Result<IReadOnlyList<FolderDto>>> Handle(
        GetFoldersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Folders
            .Where(f => f.TenantId == tenantId)
            .AsQueryable();

        if (request.ParentFolderId.HasValue)
        {
            var parentId = FolderId.From(request.ParentFolderId.Value);
            query = query.Where(f => f.ParentFolderId == parentId);
        }
        else
        {
            query = query.Where(f => f.ParentFolderId == null);
        }

        if (!string.IsNullOrWhiteSpace(request.ModuleName))
            query = query.Where(f => f.ModuleName == request.ModuleName);

        var folders = await query
            .OrderBy(f => f.Name)
            .Select(f => new FolderDto(
                f.Id.Value, f.Name, f.Path,
                f.ParentFolderId.HasValue ? f.ParentFolderId.Value.Value : null,
                f.ModuleName, f.IsSystem, f.CreatedAt))
            .ToListAsync(cancellationToken);

        if (folders.Count == 0)
            logger.LogDebug("No folders found for tenant {TenantId}", tenantId);

        return Result<IReadOnlyList<FolderDto>>.Success(folders,
            LocalizedMessage.Of("lockey_documents_folders_listed"));
    }
}
