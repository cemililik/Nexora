using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Application.Commands;
using Nexora.Modules.Documents.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Documents.Infrastructure.Services;

/// <summary>
/// Cross-module document service implementation.
/// Delegates template rendering to the existing command handler via MediatR.
/// </summary>
public sealed class DocumentService(
    DocumentsDbContext dbContext,
    IMediator mediator,
    ILogger<DocumentService> logger) : IDocumentService
{
    /// <inheritdoc />
    public async Task<GenerateFromTemplateResult?> GenerateFromTemplateAsync(
        GenerateFromTemplateRequest request, CancellationToken ct = default)
    {
        var command = new RenderDocumentTemplateCommand(
            request.TemplateId, request.FolderId, request.OutputName, request.Variables);

        var result = await mediator.Send(command, ct);

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "Template rendering failed for template {TemplateId}: {Message}",
                request.TemplateId, result.Message?.Key);
            return null;
        }

        var dto = result.Value!;
        return new GenerateFromTemplateResult(dto.DocumentId, dto.Name, dto.StorageKey);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentSummary>> GetDocumentsByEntityAsync(
        Guid entityId, string entityType, CancellationToken ct = default)
    {
        var documents = await dbContext.Documents
            .Where(d => d.LinkedEntityId == entityId && d.LinkedEntityType == entityType)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentSummary(
                d.Id.Value,
                d.Name,
                d.MimeType,
                d.FileSize,
                d.Status.ToString(),
                d.LinkedEntityId,
                d.LinkedEntityType,
                d.CreatedAt))
            .ToListAsync(ct);

        return documents;
    }
}
