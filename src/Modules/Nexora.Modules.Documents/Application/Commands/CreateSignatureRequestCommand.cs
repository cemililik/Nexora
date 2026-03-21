using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Recipient input for creating a signature request.</summary>
public sealed record SignatureRecipientInput(Guid ContactId, string Email, string Name, int SigningOrder);

/// <summary>Command to create a new signature request for a document.</summary>
public sealed record CreateSignatureRequestCommand(
    Guid DocumentId,
    string Title,
    DateOnly? ExpiresAt,
    List<SignatureRecipientInput> Recipients) : ICommand<SignatureRequestDetailDto>;

/// <summary>Validates create signature request input.</summary>
public sealed class CreateSignatureRequestValidator : AbstractValidator<CreateSignatureRequestCommand>
{
    public CreateSignatureRequestValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("lockey_documents_validation_signature_title_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_signature_title_max_length");

        RuleFor(x => x.Recipients)
            .NotEmpty().WithMessage("lockey_documents_validation_recipients_required");

        RuleForEach(x => x.Recipients).ChildRules(r =>
        {
            r.RuleFor(x => x.ContactId)
                .NotEmpty().WithMessage("lockey_documents_validation_recipient_contact_id_required");
            r.RuleFor(x => x.Email)
                .NotEmpty().WithMessage("lockey_documents_validation_recipient_email_required")
                .EmailAddress().WithMessage("lockey_documents_validation_recipient_email_invalid");
            r.RuleFor(x => x.Name)
                .NotEmpty().WithMessage("lockey_documents_validation_recipient_name_required");
            r.RuleFor(x => x.SigningOrder)
                .GreaterThan(0).WithMessage("lockey_documents_validation_recipient_signing_order_positive");
        });
    }
}

/// <summary>Creates a signature request with recipients for a document.</summary>
public sealed class CreateSignatureRequestHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateSignatureRequestHandler> logger) : ICommandHandler<CreateSignatureRequestCommand, SignatureRequestDetailDto>
{
    /// <inheritdoc />
    public async Task<Result<SignatureRequestDetailDto>> Handle(
        CreateSignatureRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
        {
            logger.LogWarning("UserId missing or invalid in tenant context for signature request creation in tenant {TenantId}", tenantId);
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
        }

        // Verify document exists
        var documentId = DocumentId.From(request.DocumentId);
        var documentExists = await dbContext.Documents
            .AnyAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (!documentExists)
        {
            logger.LogWarning("Document {DocumentId} not found for signature request in tenant {TenantId}", request.DocumentId, tenantId);
            return Result<SignatureRequestDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var signatureRequest = SignatureRequest.Create(
            tenantId, orgId, documentId, parsedUid, request.Title, request.ExpiresAt);

        foreach (var r in request.Recipients)
        {
            signatureRequest.AddRecipient(r.ContactId, r.Email, r.Name, r.SigningOrder);
        }

        await dbContext.SignatureRequests.AddAsync(signatureRequest, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Signature request {SignatureRequestId} created for document {DocumentId} with {RecipientCount} recipients in tenant {TenantId}",
            signatureRequest.Id, documentId, request.Recipients.Count, tenantId);

        var dto = MapToDetailDto(signatureRequest);
        return Result<SignatureRequestDetailDto>.Success(dto, LocalizedMessage.Of("lockey_documents_signature_request_created"));
    }

    private static SignatureRequestDetailDto MapToDetailDto(SignatureRequest sr) =>
        new(sr.Id.Value, sr.DocumentId.Value, sr.Title, sr.Status.ToString(),
            sr.ExpiresAt, sr.CompletedAt, sr.CreatedByUserId, sr.CreatedAt,
            sr.Recipients.Select(r => new SignatureRecipientDto(
                r.Id.Value, r.ContactId, r.Email, r.Name, r.SigningOrder,
                r.Status.ToString(), r.SignedAt)).ToList());
}
