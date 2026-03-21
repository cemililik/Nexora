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

/// <summary>Command to decline a signature on a request.</summary>
public sealed record DeclineSignatureCommand(Guid SignatureRequestId, Guid RecipientId) : ICommand;

/// <summary>Declines a signature for a recipient on a signature request.</summary>
public sealed class DeclineSignatureHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeclineSignatureHandler> logger) : ICommandHandler<DeclineSignatureCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(
        DeclineSignatureCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var requestId = SignatureRequestId.From(request.SignatureRequestId);

        var signatureRequest = await dbContext.SignatureRequests
            .Include(s => s.Recipients)
            .FirstOrDefaultAsync(s => s.Id == requestId && s.TenantId == tenantId, cancellationToken);

        if (signatureRequest is null)
        {
            logger.LogDebug("Signature request {SignatureRequestId} not found in tenant {TenantId}", request.SignatureRequestId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_signature_request_not_found"));
        }

        var recipientId = SignatureRecipientId.From(request.RecipientId);
        var recipient = signatureRequest.Recipients.FirstOrDefault(r => r.Id == recipientId);

        if (recipient is null)
        {
            logger.LogWarning("Recipient {RecipientId} not found in signature request {SignatureRequestId}",
                request.RecipientId, request.SignatureRequestId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_recipient_not_found"));
        }

        try
        {
            recipient.Decline();
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Cannot decline signature for recipient {RecipientId}: {Reason}", request.RecipientId, ex.Message);
            return Result.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Signature declined by recipient {RecipientId} on request {SignatureRequestId} in tenant {TenantId}",
            request.RecipientId, request.SignatureRequestId, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_documents_signature_declined"));
    }
}
