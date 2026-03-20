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

/// <summary>Command to add a new version to a document.</summary>
public sealed record AddDocumentVersionCommand(
    Guid DocumentId, string StorageKey, long FileSize, string? ChangeNote = null) : ICommand<DocumentVersionDto>;

/// <summary>Validates add document version input.</summary>
public sealed class AddDocumentVersionValidator : AbstractValidator<AddDocumentVersionCommand>
{
    private const long MaxFileSize = 52_428_800; // 50 MB

    public AddDocumentVersionValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");

        RuleFor(x => x.StorageKey)
            .NotEmpty().WithMessage("lockey_documents_validation_storage_key_required")
            .MaximumLength(1000).WithMessage("lockey_documents_validation_storage_key_max_length");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("lockey_documents_validation_file_size_positive")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("lockey_documents_validation_file_size_max");

        RuleFor(x => x.ChangeNote)
            .MaximumLength(500).WithMessage("lockey_documents_validation_change_note_max_length");
    }
}

/// <summary>Adds a new version to an existing document.</summary>
public sealed class AddDocumentVersionHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddDocumentVersionHandler> logger) : ICommandHandler<AddDocumentVersionCommand, DocumentVersionDto>
{
    public async Task<Result<DocumentVersionDto>> Handle(
        AddDocumentVersionCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentVersionDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DocumentVersionDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<DocumentVersionDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));
        var version = document.AddVersion(request.StorageKey, request.FileSize, orgId, request.ChangeNote);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Version {VersionNumber} added to document {DocumentId} for tenant {TenantId}",
            version.VersionNumber, document.Id, tenantId);

        var dto = new DocumentVersionDto(
            version.Id.Value, version.VersionNumber, version.StorageKey,
            version.FileSize, version.ChangeNote, version.UploadedByUserId, version.CreatedAt);

        return Result<DocumentVersionDto>.Success(dto, LocalizedMessage.Of("lockey_documents_version_added"));
    }
}
