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

/// <summary>Query to list documents with pagination and filtering.</summary>
public sealed record GetDocumentsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? FolderId = null,
    string? Search = null,
    string? Status = null,
    Guid? LinkedEntityId = null,
    string? LinkedEntityType = null) : IQuery<PagedResult<DocumentDto>>;

/// <summary>Returns a paginated list of documents.</summary>
public sealed class GetDocumentsHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentsHandler> logger) : IQueryHandler<GetDocumentsQuery, PagedResult<DocumentDto>>
{
    public async Task<Result<PagedResult<DocumentDto>>> Handle(
        GetDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<PagedResult<DocumentDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.Documents
            .Where(d => d.TenantId == tenantId);

        if (request.FolderId.HasValue)
        {
            var folderId = FolderId.From(request.FolderId.Value);
            query = query.Where(d => d.FolderId == folderId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(d => d.Name.Contains(request.Search));

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<DocumentStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(d => d.Status == status);

        if (request.LinkedEntityId.HasValue)
            query = query.Where(d => d.LinkedEntityId == request.LinkedEntityId);

        if (!string.IsNullOrWhiteSpace(request.LinkedEntityType))
            query = query.Where(d => d.LinkedEntityType == request.LinkedEntityType);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentDto(
                d.Id.Value, d.FolderId.Value, d.Name, d.Description,
                d.MimeType, d.FileSize, d.StorageKey, d.Status.ToString(),
                d.LinkedEntityId, d.LinkedEntityType,
                d.CurrentVersion, d.CreatedAt))
            .ToListAsync(cancellationToken);

        if (totalCount == 0)
            logger.LogDebug("No documents found for tenant {TenantId}", tenantId);

        var pagedResult = new PagedResult<DocumentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
        return Result<PagedResult<DocumentDto>>.Success(pagedResult,
            LocalizedMessage.Of("lockey_documents_documents_listed"));
    }
}
