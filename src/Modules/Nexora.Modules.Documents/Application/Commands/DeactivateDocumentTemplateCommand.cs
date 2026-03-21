using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.Modules.Documents.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Documents.Application.Commands;

/// <summary>Command to deactivate a document template.</summary>
public sealed record DeactivateDocumentTemplateCommand(Guid TemplateId) : ICommand;

/// <summary>Deactivates a document template, preventing it from being used.</summary>
public sealed class DeactivateDocumentTemplateHandler(
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeactivateDocumentTemplateHandler> logger) : ICommandHandler<DeactivateDocumentTemplateCommand>
{
    /// <inheritdoc />
    public async Task<Result> Handle(
        DeactivateDocumentTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (tenantContextAccessor.Current.TryGetTenantGuid() is not { } tenantId)
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_invalid_tenant_context"));

        var templateId = DocumentTemplateId.From(request.TemplateId);

        var template = await dbContext.DocumentTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found in tenant {TenantId}", request.TemplateId, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_documents_error_template_not_found"));
        }

        template.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Template {TemplateId} deactivated in tenant {TenantId}", template.Id, tenantId);
        return Result.Success(LocalizedMessage.Of("lockey_documents_template_deactivated"));
    }
}
