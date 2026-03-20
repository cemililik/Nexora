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

/// <summary>Command to restore an archived document.</summary>
public sealed record RestoreDocumentCommand(Guid DocumentId) : ICommand;

/// <summary>Validates restore document input.</summary>
public sealed class RestoreDocumentValidator : AbstractValidator<RestoreDocumentCommand>
{
    public RestoreDocumentValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
    }
}

/// <summary>Restores an archived document back to active status.</summary>
public sealed class RestoreDocumentHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RestoreDocumentHandler> logger) : ICommandHandler<RestoreDocumentCommand>
{
    public async Task<Result> Handle(
        RestoreDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        document.Restore();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} restored for tenant {TenantId}", document.Id, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_document_restored"));
    }
}
