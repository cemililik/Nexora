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

/// <summary>Command to update document metadata.</summary>
public sealed record UpdateDocumentMetadataCommand(
    Guid DocumentId,
    string Name,
    string? Description = null,
    string? Tags = null) : ICommand<DocumentDto>;

/// <summary>Validates document metadata update input.</summary>
public sealed class UpdateDocumentMetadataValidator : AbstractValidator<UpdateDocumentMetadataCommand>
{
    public UpdateDocumentMetadataValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_documents_validation_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_name_max_length");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("lockey_documents_validation_description_max_length");
    }
}

/// <summary>Updates document name, description, and tags.</summary>
public sealed class UpdateDocumentMetadataHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateDocumentMetadataHandler> logger) : ICommandHandler<UpdateDocumentMetadataCommand, DocumentDto>
{
    public async Task<Result<DocumentDto>> Handle(
        UpdateDocumentMetadataCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        document.UpdateMetadata(request.Name, request.Description, request.Tags);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} metadata updated for tenant {TenantId}", document.Id, tenantId);

        var dto = new DocumentDto(
            document.Id.Value, document.FolderId.Value, document.Name, document.Description,
            document.MimeType, document.FileSize, document.StorageKey, document.Status.ToString(),
            document.LinkedEntityId, document.LinkedEntityType,
            document.CurrentVersion, document.CreatedAt);

        return Result<DocumentDto>.Success(dto, LocalizedMessage.Of("lockey_documents_document_updated"));
    }
}
