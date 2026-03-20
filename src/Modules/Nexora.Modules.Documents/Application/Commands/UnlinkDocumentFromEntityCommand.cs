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

/// <summary>Command to unlink a document from its entity.</summary>
public sealed record UnlinkDocumentFromEntityCommand(Guid DocumentId) : ICommand;

/// <summary>Validates unlink document input.</summary>
public sealed class UnlinkDocumentFromEntityValidator : AbstractValidator<UnlinkDocumentFromEntityCommand>
{
    public UnlinkDocumentFromEntityValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
    }
}

/// <summary>Removes the entity link from a document.</summary>
public sealed class UnlinkDocumentFromEntityHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UnlinkDocumentFromEntityHandler> logger) : ICommandHandler<UnlinkDocumentFromEntityCommand>
{
    public async Task<Result> Handle(
        UnlinkDocumentFromEntityCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        document.UnlinkEntity();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} unlinked from entity for tenant {TenantId}", document.Id, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_document_unlinked"));
    }
}
