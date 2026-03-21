using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Domain.Exceptions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to update an existing document template.</summary>
public sealed record UpdateDocumentTemplateCommand(
    Guid TemplateId,
    string Name,
    string Category,
    string Format,
    string? VariableDefinitions = null) : ICommand<DocumentTemplateDetailDto>;

/// <summary>Validates document template update input.</summary>
public sealed class UpdateDocumentTemplateValidator : AbstractValidator<UpdateDocumentTemplateCommand>
{
    public UpdateDocumentTemplateValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("lockey_documents_validation_template_id_required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_documents_validation_template_name_required")
            .MaximumLength(500).WithMessage("lockey_documents_validation_template_name_max_length");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("lockey_documents_validation_template_category_required")
            .Must(c => Enum.TryParse<TemplateCategory>(c, true, out _))
            .WithMessage("lockey_documents_validation_template_category_invalid");

        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_documents_validation_template_format_required")
            .Must(f => Enum.TryParse<TemplateFormat>(f, true, out _))
            .WithMessage("lockey_documents_validation_template_format_invalid");
    }
}

/// <summary>Updates an existing document template's metadata.</summary>
public sealed class UpdateDocumentTemplateHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateDocumentTemplateHandler> logger) : ICommandHandler<UpdateDocumentTemplateCommand, DocumentTemplateDetailDto>
{
    /// <inheritdoc />
    public async Task<Result<DocumentTemplateDetailDto>> Handle(
        UpdateDocumentTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var templateId = DocumentTemplateId.From(request.TemplateId);

        var template = await dbContext.DocumentTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found in tenant {TenantId}", request.TemplateId, tenantId);
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_template_not_found"));
        }

        var category = Enum.Parse<TemplateCategory>(request.Category, true);
        var format = Enum.Parse<TemplateFormat>(request.Format, true);

        try
        {
            template.Update(request.Name, category, format, request.VariableDefinitions);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Cannot update template {TemplateId}: {Reason}", request.TemplateId, ex.Message);
            return Result<DocumentTemplateDetailDto>.Failure(LocalizedMessage.Of(ex.LocalizationKey));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document template {TemplateId} updated in tenant {TenantId}", template.Id, tenantId);

        return Result<DocumentTemplateDetailDto>.Success(
            new DocumentTemplateDetailDto(
                template.Id.Value, template.Name, template.Category.ToString(), template.Format.ToString(),
                template.TemplateStorageKey, template.VariableDefinitions, template.IsActive,
                template.CreatedAt, template.UpdatedAt),
            LocalizedMessage.Of("lockey_documents_template_updated"));
    }
}
