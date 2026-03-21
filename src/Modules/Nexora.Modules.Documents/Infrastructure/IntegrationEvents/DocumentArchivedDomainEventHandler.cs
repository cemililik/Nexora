using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentArchivedEvent and publishes integration event.</summary>
public sealed class DocumentArchivedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ILogger<DocumentArchivedDomainEventHandler> logger) : INotificationHandler<DocumentArchivedEvent>
{
    /// <summary>
    /// Handles a <see cref="DocumentArchivedEvent"/> by publishing a <see cref="DocumentArchivedIntegrationEvent"/> to the event bus.
    /// Reads TenantId from the document entity (same transaction scope as the domain event).
    /// </summary>
    public async Task Handle(DocumentArchivedEvent notification, CancellationToken cancellationToken)
    {
        var tenantId = await dbContext.Documents
            .AsNoTracking()
            .Where(d => d.Id == notification.DocumentId)
            .Select(d => d.TenantId.ToString())
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId is null)
        {
            logger.LogWarning("Document {DocumentId} not found for DocumentArchivedEvent, skipping integration event",
                notification.DocumentId.Value);
            return;
        }

        var integrationEvent = new DocumentArchivedIntegrationEvent
        {
            TenantId = tenantId,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
