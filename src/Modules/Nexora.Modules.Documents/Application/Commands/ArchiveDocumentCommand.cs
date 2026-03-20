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

/// <summary>Command to archive a document.</summary>
public sealed record ArchiveDocumentCommand(Guid DocumentId) : ICommand;

/// <summary>Validates archive document input.</summary>
public sealed class ArchiveDocumentValidator : AbstractValidator<ArchiveDocumentCommand>
{
    public ArchiveDocumentValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
    }
}

/// <summary>Archives a document by changing its status.</summary>
public sealed class ArchiveDocumentHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ArchiveDocumentHandler> logger) : ICommandHandler<ArchiveDocumentCommand>
{
    public async Task<Result> Handle(
        ArchiveDocumentCommand request,
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

        document.Archive();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} archived for tenant {TenantId}", document.Id, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_document_archived"));
    }
}
