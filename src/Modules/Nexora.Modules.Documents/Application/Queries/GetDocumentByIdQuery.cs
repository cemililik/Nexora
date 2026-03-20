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

/// <summary>Query to get document detail by ID including versions and access list.</summary>
public sealed record GetDocumentByIdQuery(Guid DocumentId) : IQuery<DocumentDetailDto>;

/// <summary>Returns document detail with versions and access list.</summary>
public sealed class GetDocumentByIdHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentByIdHandler> logger) : IQueryHandler<GetDocumentByIdQuery, DocumentDetailDto>
{
    public async Task<Result<DocumentDetailDto>> Handle(
        GetDocumentByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .Include(d => d.Versions)
            .Include(d => d.AccessList)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogDebug("Document {DocumentId} not found", request.DocumentId);
            return Result<DocumentDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var folder = await dbContext.Folders.FirstOrDefaultAsync(
            f => f.Id == document.FolderId, cancellationToken);

        var versionDtos = document.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(
                v.Id.Value, v.VersionNumber, v.StorageKey,
                v.FileSize, v.ChangeNote, v.UploadedByUserId, v.CreatedAt))
            .ToList();

        var accessDtos = document.AccessList
            .Select(a => new DocumentAccessDto(
                a.Id.Value, a.UserId, a.RoleId, a.Permission.ToString()))
            .ToList();

        var dto = new DocumentDetailDto(
            document.Id.Value, document.FolderId.Value,
            folder?.Name ?? string.Empty,
            document.Name, document.Description,
            document.MimeType, document.FileSize, document.StorageKey,
            document.Status.ToString(),
            document.LinkedEntityId, document.LinkedEntityType,
            document.CurrentVersion, document.Tags,
            document.UploadedByUserId, document.CreatedAt, document.UpdatedAt,
            versionDtos, accessDtos);

        return Result<DocumentDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_documents_document_retrieved"));
    }
}
