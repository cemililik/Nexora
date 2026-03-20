using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentCreatedEvent and publishes integration event.</summary>
public sealed class DocumentCreatedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ILogger<DocumentCreatedDomainEventHandler> logger) : INotificationHandler<DocumentCreatedEvent>
{
    /// <summary>
    /// Handles a <see cref="DocumentCreatedEvent"/> by publishing a <see cref="DocumentUploadedIntegrationEvent"/> to the event bus.
    /// Logs a warning and skips if the document is not found in the database.
    /// </summary>
    public async Task Handle(DocumentCreatedEvent notification, CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == notification.DocumentId, cancellationToken);

        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} not found for domain event, skipping integration event", notification.DocumentId);
            return;
        }

        var integrationEvent = new DocumentUploadedIntegrationEvent
        {
            TenantId = document.TenantId.ToString(),
            DocumentId = document.Id.Value,
            Name = document.Name,
            MimeType = document.MimeType,
            FileSize = document.FileSize,
            FolderId = document.FolderId.Value,
            LinkedEntityId = document.LinkedEntityId,
            LinkedEntityType = document.LinkedEntityType
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
