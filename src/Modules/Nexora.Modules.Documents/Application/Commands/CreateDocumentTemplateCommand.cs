using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.Entities;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to create a new document template.</summary>
public sealed record CreateDocumentTemplateCommand(
    string Name,
    string Category,
    string Format,
    string TemplateStorageKey,
    string? VariableDefinitions = null) : ICommand<DocumentTemplateDetailDto>;

/// <summary>Validates document template creation input.</summary>
public sealed class CreateDocumentTemplateValidator : AbstractValidator<CreateDocumentTemplateCommand>
{
    public CreateDocumentTemplateValidator()
    {
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

        RuleFor(x => x.TemplateStorageKey)
            .NotEmpty().WithMessage("lockey_documents_validation_storage_key_required")
            .MaximumLength(1000).WithMessage("lockey_documents_validation_storage_key_max_length");
    }
}

/// <summary>Creates a new document template.</summary>
public sealed class CreateDocumentTemplateHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateDocumentTemplateHandler> logger) : ICommandHandler<CreateDocumentTemplateCommand, DocumentTemplateDetailDto>
{
    /// <inheritdoc />
    public async Task<Result<DocumentTemplateDetailDto>> Handle(
        CreateDocumentTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        if (tenantContextAccessor.Current.TryGetOrganizationGuid() is not { } orgId)
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_organization_context"));

        var category = Enum.Parse<TemplateCategory>(request.Category, true);
        var format = Enum.Parse<TemplateFormat>(request.Format, true);

        var template = DocumentTemplate.Create(
            tenantId, orgId, request.Name, category, format,
            request.TemplateStorageKey, request.VariableDefinitions);

        await dbContext.DocumentTemplates.AddAsync(template, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Document template {TemplateId} created in tenant {TenantId}", template.Id, tenantId);

        return Result<DocumentTemplateDetailDto>.Success(
            MapToDetailDto(template),
            LocalizedMessage.Of("lockey_documents_template_created"));
    }

    private static DocumentTemplateDetailDto MapToDetailDto(DocumentTemplate t) =>
        new(t.Id.Value, t.Name, t.Category.ToString(), t.Format.ToString(),
            t.TemplateStorageKey, t.VariableDefinitions, t.IsActive,
            t.CreatedAt, t.UpdatedAt);
}
