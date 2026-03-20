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

/// <summary>Query to get all versions of a document.</summary>
public sealed record GetDocumentVersionsQuery(Guid DocumentId) : IQuery<IReadOnlyList<DocumentVersionDto>>;

/// <summary>Returns all versions of a document ordered by version number.</summary>
public sealed class GetDocumentVersionsHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentVersionsHandler> logger) : IQueryHandler<GetDocumentVersionsQuery, IReadOnlyList<DocumentVersionDto>>
{
    public async Task<Result<IReadOnlyList<DocumentVersionDto>>> Handle(
        GetDocumentVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<IReadOnlyList<DocumentVersionDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);

        var documentExists = await dbContext.Documents
            .AnyAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (!documentExists)
        {
            logger.LogDebug("Document {DocumentId} not found", request.DocumentId);
            return Result<IReadOnlyList<DocumentVersionDto>>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var versions = await dbContext.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(
                v.Id.Value, v.VersionNumber, v.StorageKey,
                v.FileSize, v.ChangeNote, v.UploadedByUserId, v.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DocumentVersionDto>>.Success(versions,
            LocalizedMessage.Of("lockey_documents_versions_listed"));
    }
}
