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

/// <summary>Query to retrieve paginated document templates with optional filters.</summary>
public sealed record GetDocumentTemplatesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Category = null,
    bool? IsActive = null) : IQuery<PagedResult<DocumentTemplateDto>>;

/// <summary>Retrieves paginated document templates filtered by tenant, category, and active status.</summary>
public sealed class GetDocumentTemplatesHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentTemplatesHandler> logger) : IQueryHandler<GetDocumentTemplatesQuery, PagedResult<DocumentTemplateDto>>
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<DocumentTemplateDto>>> Handle(
        GetDocumentTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<PagedResult<DocumentTemplateDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var query = dbContext.DocumentTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Category) &&
            Enum.TryParse<TemplateCategory>(request.Category, true, out var categoryFilter))
        {
            query = query.Where(t => t.Category == categoryFilter);
        }

        if (request.IsActive is not null)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new DocumentTemplateDto(
                t.Id.Value, t.Name, t.Category.ToString(), t.Format.ToString(),
                t.IsActive, t.CreatedAt))
            .ToListAsync(cancellationToken);

        if (totalCount == 0)
            logger.LogDebug("No document templates found for tenant {TenantId}", tenantId);

        return Result<PagedResult<DocumentTemplateDto>>.Success(
            new PagedResult<DocumentTemplateDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
    }
}
