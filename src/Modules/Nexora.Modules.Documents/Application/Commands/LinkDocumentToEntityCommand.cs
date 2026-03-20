using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to link a document to an entity.</summary>
public sealed record LinkDocumentToEntityCommand(
    Guid DocumentId, Guid EntityId, string EntityType) : ICommand<DocumentDto>;

/// <summary>Validates link document to entity input.</summary>
public sealed class LinkDocumentToEntityValidator : AbstractValidator<LinkDocumentToEntityCommand>
{
    public LinkDocumentToEntityValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("lockey_documents_validation_entity_id_required");
        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("lockey_documents_validation_entity_type_required")
            .MaximumLength(100).WithMessage("lockey_documents_validation_entity_type_max_length");
    }
}

/// <summary>Links a document to a specific entity.</summary>
public sealed class LinkDocumentToEntityHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<LinkDocumentToEntityHandler> logger) : ICommandHandler<LinkDocumentToEntityCommand, DocumentDto>
{
    public async Task<Result<DocumentDto>> Handle(
        LinkDocumentToEntityCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DocumentDto>.Failure(LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        document.LinkToEntity(request.EntityId, request.EntityType);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} linked to {EntityType} {EntityId} for tenant {TenantId}",
            document.Id, request.EntityType, request.EntityId, tenantId);

        var dto = new DocumentDto(
            document.Id.Value, document.FolderId.Value, document.Name, document.Description,
            document.MimeType, document.FileSize, document.StorageKey, document.Status.ToString(),
            document.LinkedEntityId, document.LinkedEntityType,
            document.CurrentVersion, document.CreatedAt);

        return Result<DocumentDto>.Success(dto, LocalizedMessage.Of("lockey_documents_document_linked"));
    }
}
