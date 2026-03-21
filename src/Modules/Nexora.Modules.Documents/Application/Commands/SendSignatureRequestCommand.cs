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

/// <summary>Command to send a draft signature request to recipients.</summary>
public sealed record SendSignatureRequestCommand(Guid SignatureRequestId) : ICommand;

/// <summary>Transitions a signature request from Draft to Sent status.</summary>
public sealed class SendSignatureRequestHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SendSignatureRequestHandler> logger) : ICommandHandler<SendSignatureRequestCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(
        SendSignatureRequestCommand request,
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
            logger.LogWarning("Signature request {SignatureRequestId} not found in tenant {TenantId}", request.SignatureRequestId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_signature_request_not_found"));
        }

        try
        {
            signatureRequest.Send();
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Cannot send signature request {SignatureRequestId}: {Reason}", request.SignatureRequestId, ex.Message);
            return Result.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Signature request {SignatureRequestId} sent in tenant {TenantId}", request.SignatureRequestId, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_signature_request_sent"));
    }
}
