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

/// <summary>Query to get access permissions for a document.</summary>
public sealed record GetDocumentAccessQuery(Guid DocumentId) : IQuery<IReadOnlyList<DocumentAccessDto>>;

/// <summary>Returns all access permissions for a document.</summary>
public sealed class GetDocumentAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentAccessHandler> logger) : IQueryHandler<GetDocumentAccessQuery, IReadOnlyList<DocumentAccessDto>>
{
    public async Task<Result<IReadOnlyList<DocumentAccessDto>>> Handle(
        GetDocumentAccessQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var documentId = DocumentId.From(request.DocumentId);

        var documentExists = await dbContext.Documents
            .AnyAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (!documentExists)
        {
            logger.LogDebug("Document {DocumentId} not found", request.DocumentId);
            return Result<IReadOnlyList<DocumentAccessDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var accessList = await dbContext.DocumentAccesses
            .Where(a => a.DocumentId == documentId)
            .Select(a => new DocumentAccessDto(
                a.Id.Value, a.UserId, a.RoleId, a.Permission.ToString()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DocumentAccessDto>>.Success(accessList,
            LocalizedMessage.Of("lockey_documents_access_listed"));
    }
}
