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

/// <summary>Command to move a document to another folder.</summary>
public sealed record MoveDocumentCommand(Guid DocumentId, Guid TargetFolderId) : ICommand<DocumentDto>;

/// <summary>Validates move document input.</summary>
public sealed class MoveDocumentValidator : AbstractValidator<MoveDocumentCommand>
{
    public MoveDocumentValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");
        RuleFor(x => x.TargetFolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");
    }
}

/// <summary>Moves a document to the specified target folder.</summary>
public sealed class MoveDocumentHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<MoveDocumentHandler> logger) : ICommandHandler<MoveDocumentCommand, DocumentDto>
{
    public async Task<Result<DocumentDto>> Handle(
        MoveDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);
        var targetFolderId = FolderId.From(request.TargetFolderId);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DocumentDto>.Failure(LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var folderExists = await dbContext.Folders
            .AnyAsync(f => f.Id == targetFolderId && f.TenantId == tenantId, cancellationToken);

        if (!folderExists)
        {
            logger.LogWarning("Target folder {FolderId} not found for tenant {TenantId}", request.TargetFolderId, tenantId);
            return Result<DocumentDto>.Failure(LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        document.MoveToFolder(targetFolderId);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document {DocumentId} moved to folder {FolderId} for tenant {TenantId}",
            document.Id, targetFolderId, tenantId);

        var dto = new DocumentDto(
            document.Id.Value, document.FolderId.Value, document.Name, document.Description,
            document.MimeType, document.FileSize, document.StorageKey, document.Status.ToString(),
            document.LinkedEntityId, document.LinkedEntityType,
            document.CurrentVersion, document.CreatedAt);

        return Result<DocumentDto>.Success(dto, LocalizedMessage.Of("lockey_documents_document_moved"));
    }
}
