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

/// <summary>Query to retrieve a signature request by its identifier with recipient details.</summary>
public sealed record GetSignatureRequestByIdQuery(Guid SignatureRequestId) : IQuery<SignatureRequestDetailDto>;

/// <summary>Retrieves a single signature request with its recipients.</summary>
public sealed class GetSignatureRequestByIdHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetSignatureRequestByIdHandler> logger) : IQueryHandler<GetSignatureRequestByIdQuery, SignatureRequestDetailDto>
{
    /// <inheritdoc />
    public async Task<Result<SignatureRequestDetailDto>> Handle(
        GetSignatureRequestByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var requestId = SignatureRequestId.From(request.SignatureRequestId);

        var signatureRequest = await dbContext.SignatureRequests
            .AsNoTracking()
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.Id == requestId && s.TenantId == tenantId, cancellationToken);

        if (signatureRequest is null)
        {
            logger.LogDebug("Signature request {SignatureRequestId} not found in tenant {TenantId}", request.SignatureRequestId, tenantId);
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_signature_request_not_found"));
        }

        var dto = new SignatureRequestDetailDto(
            signatureRequest.Id.Value,
            signatureRequest.DocumentId.Value,
            signatureRequest.Title,
            signatureRequest.Status.ToString(),
            signatureRequest.ExpiresAt,
            signatureRequest.CompletedAt,
            signatureRequest.CreatedByUserId,
            signatureRequest.CreatedAt,
            signatureRequest.Recipients
                .OrderBy(r => r.SigningOrder)
                .Select(r => new SignatureRecipientDto(
                    r.Id.Value, r.ContactId, r.Email, r.Name, r.SigningOrder,
                    r.Status.ToString(), r.SignedAt))
                .ToList());

        return Result<SignatureRequestDetailDto>.Success(dto);
    }
}
