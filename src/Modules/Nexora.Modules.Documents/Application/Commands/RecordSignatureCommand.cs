using FluentValidation;
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

/// <summary>Command to record a recipient's signature on a request.</summary>
public sealed record RecordSignatureCommand(
    Guid SignatureRequestId,
    Guid RecipientId,
    string SignatureData,
    string IpAddress) : ICommand;

/// <summary>Validates signature recording input.</summary>
public sealed class RecordSignatureValidator : AbstractValidator<RecordSignatureCommand>
{
    public RecordSignatureValidator()
    {
        RuleFor(x => x.SignatureRequestId)
            .NotEmpty().WithMessage("lockey_documents_validation_signature_request_id_required");

        RuleFor(x => x.RecipientId)
            .NotEmpty().WithMessage("lockey_documents_validation_recipient_id_required");

        RuleFor(x => x.SignatureData)
            .NotEmpty().WithMessage("lockey_documents_validation_signature_data_required")
            .MaximumLength(500_000).WithMessage("lockey_documents_validation_signature_data_max_length");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("lockey_documents_validation_ip_address_required")
            .MaximumLength(45).WithMessage("lockey_documents_validation_ip_address_max_length")
            .Matches(@"^[\d.:a-fA-F]+$").WithMessage("lockey_documents_validation_ip_address_format");
    }
}

/// <summary>Records a signature from a recipient on a signature request.</summary>
public sealed class RecordSignatureHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RecordSignatureHandler> logger) : ICommandHandler<RecordSignatureCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(
        RecordSignatureCommand request,
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

        var recipientId = SignatureRecipientId.From(request.RecipientId);

        try
        {
            signatureRequest.RecordSignature(recipientId, request.SignatureData, request.IpAddress);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Cannot record signature for recipient {RecipientId} on request {SignatureRequestId}: {Reason}",
                request.RecipientId, request.SignatureRequestId, ex.Message);
            return Result.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Signature recorded for recipient {RecipientId} on request {SignatureRequestId} in tenant {TenantId}",
            request.RecipientId, request.SignatureRequestId, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_documents_signature_recorded"));
    }
}
