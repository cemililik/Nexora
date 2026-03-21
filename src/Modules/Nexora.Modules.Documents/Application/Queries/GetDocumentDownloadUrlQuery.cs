using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Application.Services;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Abstractions.Storage;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Queries;

/// <summary>Query to generate a presigned download URL for a document.</summary>
public sealed record GetDocumentDownloadUrlQuery(Guid DocumentId) : IQuery<DownloadUrlDto>;

/// <summary>Generates a presigned download URL for the specified document.</summary>
public sealed class GetDocumentDownloadUrlHandler(
    DocumentsDbContext dbContext,
    IFileStorageService fileStorageService,
    IDocumentAccessChecker accessChecker,
    ITenantContextAccessor tenantContextAccessor,
    IOptions<StorageOptions> storageOptions,
    ILogger<GetDocumentDownloadUrlHandler> logger) : IQueryHandler<GetDocumentDownloadUrlQuery, DownloadUrlDto>
{
    /// <inheritdoc />
    public async Task<Result<DownloadUrlDto>> Handle(
        GetDocumentDownloadUrlQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DownloadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var userId))
            return Result<DownloadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_user_context"));

        var documentId = DocumentId.From(request.DocumentId);

        // Check access before generating download URL
        var hasAccess = await accessChecker.HasAccessAsync(documentId, userId, tenantId, ct: cancellationToken);
        if (!hasAccess)
        {
            logger.LogDebug("User {UserId} denied download access to document {DocumentId}", userId, request.DocumentId);
            return Result<DownloadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_access_denied"));
        }

        var document = await dbContext.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.TenantId == tenantId)
            .Select(d => new { d.StorageKey, d.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            logger.LogDebug("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DownloadUrlDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var opts = storageOptions.Value;
        var bucketName = $"{opts.BucketPrefix}-{tenantId}";

        var result = await fileStorageService.GenerateDownloadPresignedUrlAsync(
            bucketName,
            document.StorageKey,
            opts.DefaultPresignedUrlExpiry,
            cancellationToken);

        logger.LogDebug(
            "Generated download URL for document {DocumentId} in tenant {TenantId}",
            request.DocumentId, tenantId);

        var dto = new DownloadUrlDto(result.Url, result.ExpiresAt);
        return Result<DownloadUrlDto>.Success(dto);
    }
}
