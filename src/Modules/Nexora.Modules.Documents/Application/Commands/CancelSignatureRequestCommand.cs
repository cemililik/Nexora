using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to cancel a signature request.</summary>
public sealed record CancelSignatureRequestCommand(Guid SignatureRequestId) : ICommand;

/// <summary>Cancels a signature request that is not yet completed or expired.</summary>
public sealed class CancelSignatureRequestHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CancelSignatureRequestHandler> logger) : ICommandHandler<CancelSignatureRequestCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(
        CancelSignatureRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var requestId = SignatureRequestId.From(request.SignatureRequestId);

        var signatureRequest = await dbContext.SignatureRequests
            .FirstOrDefaultAsync(s => s.Id == requestId && s.TenantId == tenantId, cancellationToken);

        if (signatureRequest is null)
        {
            logger.LogWarning("Signature request {SignatureRequestId} not found in tenant {TenantId}", request.SignatureRequestId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_signature_request_not_found"));
        }

        try
        {
            signatureRequest.Cancel();
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Cannot cancel signature request {SignatureRequestId}: {Reason}", request.SignatureRequestId, ex.Message);
            return Result.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Signature request {SignatureRequestId} cancelled in tenant {TenantId}", request.SignatureRequestId, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_signature_request_cancelled"));
    }
}
