using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.DTOs;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Queries;

/// <summary>Query to retrieve a document template by its identifier.</summary>
public sealed record GetDocumentTemplateByIdQuery(Guid TemplateId) : IQuery<DocumentTemplateDetailDto>;

/// <summary>Retrieves a single document template with variable definitions.</summary>
public sealed class GetDocumentTemplateByIdHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetDocumentTemplateByIdHandler> logger) : IQueryHandler<GetDocumentTemplateByIdQuery, DocumentTemplateDetailDto>
{
    /// <inheritdoc />
    public async Task<Result<DocumentTemplateDetailDto>> Handle(
        GetDocumentTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var templateId = DocumentTemplateId.From(request.TemplateId);

        var template = await dbContext.DocumentTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogDebug("Template {TemplateId} not found in tenant {TenantId}", request.TemplateId, tenantId);
            return Result<DocumentTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_documents_error_template_not_found"));
        }

        var dto = new DocumentTemplateDetailDto(
            template.Id.Value, template.Name, template.Category.ToString(), template.Format.ToString(),
            template.TemplateStorageKey, template.VariableDefinitions, template.IsActive,
            template.CreatedAt, template.UpdatedAt);

        return Result<DocumentTemplateDetailDto>.Success(dto);
    }
}
