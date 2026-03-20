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

/// <summary>Command to grant access permission on a document.</summary>
public sealed record GrantDocumentAccessCommand(
    Guid DocumentId, Guid? UserId, Guid? RoleId, string Permission) : ICommand<DocumentAccessDto>;

/// <summary>Validates grant access input.</summary>
public sealed class GrantDocumentAccessValidator : AbstractValidator<GrantDocumentAccessCommand>
{
    public GrantDocumentAccessValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("lockey_documents_validation_document_id_required");

        RuleFor(x => x.Permission)
            .NotEmpty().WithMessage("lockey_documents_validation_permission_required")
            .Must(p => p is "View" or "Edit" or "Manage")
            .WithMessage("lockey_documents_validation_permission_invalid");

        RuleFor(x => x)
            .Must(x => x.UserId.HasValue || x.RoleId.HasValue)
            .WithMessage("lockey_documents_validation_user_or_role_required");
    }
}

/// <summary>Grants access permission on a document to a user or role.</summary>
public sealed class GrantDocumentAccessHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GrantDocumentAccessHandler> logger) : ICommandHandler<GrantDocumentAccessCommand, DocumentAccessDto>
{
    public async Task<Result<DocumentAccessDto>> Handle(
        GrantDocumentAccessCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var documentId = DocumentId.From(request.DocumentId);

        var document = await dbContext.Documents
            .Include(d => d.AccessList)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for tenant {TenantId}", request.DocumentId, tenantId);
            return Result<DocumentAccessDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_document_not_found"));
        }

        var permission = Enum.Parse<AccessPermission>(request.Permission, ignoreCase: true);
        var access = document.GrantAccess(request.UserId, request.RoleId, permission);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Access granted on document {DocumentId} for tenant {TenantId}", document.Id, tenantId);

        var dto = new DocumentAccessDto(
            access.Id.Value, access.UserId, access.RoleId, access.Permission.ToString());

        return Result<DocumentAccessDto>.Success(dto, LocalizedMessage.Of("lockey_documents_access_granted"));
    }
}
