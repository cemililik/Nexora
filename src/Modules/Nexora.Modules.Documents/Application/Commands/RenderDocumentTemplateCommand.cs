using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.Services;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to render a document from a template with variable substitution.</summary>
public sealed record RenderDocumentTemplateCommand(
    Guid TemplateId,
    Guid FolderId,
    string OutputName,
    Dictionary<string, string> Variables) : ICommand<RenderTemplateResultDto>;

/// <summary>Validates template rendering input.</summary>
public sealed class RenderDocumentTemplateValidator : AbstractValidator<RenderDocumentTemplateCommand>
{
    public RenderDocumentTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("lockey_documents_validation_template_id_required");

        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("lockey_documents_validation_folder_id_required");

        RuleFor(x => x.OutputName)
            .NotEmpty().WithMessage("lockey_documents_validation_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_name_max_length")
            .Must(n => !n.Contains("..") && !n.Contains('/') && !n.Contains('\\'))
            .WithMessage("lockey_documents_validation_name_invalid_characters");
    }
}

/// <summary>Renders a document from a template by substituting variables and creating a document record.</summary>
public sealed class RenderDocumentTemplateHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RenderDocumentTemplateHandler> logger) : ICommandHandler<RenderDocumentTemplateCommand, RenderTemplateResultDto>
{
    /// <inheritdoc />
    public async Task<Result<RenderTemplateResultDto>> Handle(
        RenderDocumentTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        if (tenantContextAccessor.Current.UserId is not { } uid || !Guid.TryParse(uid, out var parsedUid))
        {
            logger.LogWarning("UserId missing or invalid for template rendering in tenant {TenantId}", tenantId);
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_missing_user_context"));
        }

        // Verify template exists and is active
        var templateId = DocumentTemplateId.From(request.TemplateId);
        var template = await dbContext.DocumentTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found in tenant {TenantId}", request.TemplateId, tenantId);
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_template_not_found"));
        }

        if (!template.IsActive)
        {
            logger.LogWarning("Template {TemplateId} is inactive", request.TemplateId);
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_template_inactive"));
        }

        // Verify folder exists within same organization
        var folderId = FolderId.From(request.FolderId);
        var folderExists = await dbContext.Folders
            .AnyAsync(f => f.Id == folderId && f.TenantId == tenantId && f.OrganizationId == orgId, cancellationToken);

        if (!folderExists)
        {
            logger.LogWarning("Folder {FolderId} not found for template rendering in tenant {TenantId}", request.FolderId, tenantId);
            return Result<RenderTemplateResultDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_folder_not_found"));
        }

        // Validate required variables
        try
        {
            TemplateVariableRenderer.ValidateVariables(request.Variables, template.VariableDefinitions);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Variable validation failed for template {TemplateId}: {Reason}", request.TemplateId, ex.Message);
            return Result<RenderTemplateResultDto>.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        // Generate rendered document storage key
        var mimeType = template.Format switch
        {
            TemplateFormat.Pdf => "application/pdf",
            TemplateFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TemplateFormat.Html => "text/html",
            _ => "application/octet-stream"
        };
        var storageKey = $"{orgId}/documents/{Guid.NewGuid()}/{request.OutputName}";

        // Create document record in PendingRender status — a background worker picks up the
        // DocumentRenderRequestedEvent to perform actual file rendering and transition to Active.
        var document = Document.CreatePendingRender(
            tenantId, orgId, folderId, parsedUid,
            request.OutputName, mimeType, storageKey, templateId,
            $"Generated from template: {template.Name}");

        await dbContext.Documents.AddAsync(document, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Document {DocumentId} rendered from template {TemplateId} in tenant {TenantId}",
            document.Id, templateId, tenantId);

        var dto = new RenderTemplateResultDto(document.Id.Value, request.OutputName, storageKey);
        return Result<RenderTemplateResultDto>.Success(dto, LocalizedMessage.Of("lockey_documents_template_rendered"));
    }
}
