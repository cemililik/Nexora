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

/// <summary>Query to retrieve paginated signature requests with optional filters.</summary>
public sealed record GetSignatureRequestsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? DocumentId = null,
    string? Status = null) : IQuery<PagedResult<SignatureRequestDto>>;

/// <summary>Retrieves paginated signature requests filtered by tenant, document, and status.</summary>
public sealed class GetSignatureRequestsHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetSignatureRequestsHandler> logger) : IQueryHandler<GetSignatureRequestsQuery, PagedResult<SignatureRequestDto>>
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<SignatureRequestDto>>> Handle(
        GetSignatureRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<PagedResult<SignatureRequestDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var query = dbContext.SignatureRequests
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        if (request.DocumentId is not null)
        {
            var docId = DocumentId.From(request.DocumentId.Value);
            query = query.Where(s => s.DocumentId == docId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<SignatureRequestStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(s => s.Status == statusFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SignatureRequestDto(
                s.Id.Value,
                s.DocumentId.Value,
                s.Title,
                s.Status.ToString(),
                s.ExpiresAt,
                s.Recipients.Count,
                s.Recipients.Count(r => r.Status == SignatureRecipientStatus.Signed),
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        if (totalCount == 0)
            logger.LogDebug("No signature requests found for tenant {TenantId}", tenantId);

        return Result<PagedResult<SignatureRequestDto>>.Success(
            new PagedResult<SignatureRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
    }
}
