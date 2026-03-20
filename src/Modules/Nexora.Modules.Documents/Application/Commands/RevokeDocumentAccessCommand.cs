using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to revoke access permission on a document.</summary>
public sealed record RevokeDocumentAccessCommand(Guid DocumentId, Guid AccessId) : ICommand;

/// <summary>Validates revoke access input.</summary>
public sealed class RevokeDocumentAccessValidator : AbstractValidator<RevokeDocumentAccessCommand>
{
    public RevokeDocumentAccessValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
        RuleFor(x => x.AccessId)
            .NotEmpty().WithMessage("lockey_documents_validation_access_id_required");
    }
}

/// <summary>Revokes an access permission from a document.</summary>
public sealed class RevokeDocumentAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RevokeDocumentAccessHandler> logger) : ICommandHandler<RevokeDocumentAccessCommand>
{
    public async Task<Result> Handle(
        RevokeDocumentAccessCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .Include(d => d.AccessList)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var accessId = DocumentAccessId.From(request.AccessId);
        document.RevokeAccess(accessId);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Access {AccessId} revoked on document {DocumentId} for tenant {TenantId}",
            request.AccessId, document.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_documents_access_revoked"));
    }
}
