using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentArchivedEvent and publishes integration event.</summary>
public sealed class DocumentArchivedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DocumentArchivedDomainEventHandler> logger) : INotificationHandler<DocumentArchivedEvent>
{
    /// <summary>
    /// Handles a <see cref="DocumentArchivedEvent"/> by publishing a <see cref="DocumentArchivedIntegrationEvent"/> to the event bus.
    /// Falls back to loading TenantId from the database when tenant context is unavailable.
    /// </summary>
    public async Task Handle(DocumentArchivedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        string? tenantId = tenantContext?.TenantId;

        if (tenantId is null)
        {
            logger.LogWarning("Tenant context unavailable when handling DocumentArchivedEvent for document {DocumentId}, falling back to DB lookup",
                notification.DocumentId.Value);

            var document = await dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == notification.DocumentId, cancellationToken);

            if (document is null)
            {
                logger.LogWarning("Document {DocumentId} not found for DocumentArchivedEvent, skipping integration event",
                    notification.DocumentId.Value);
                return;
            }

            tenantId = document.TenantId.ToString();
        }

        var integrationEvent = new DocumentArchivedIntegrationEvent
        {
            TenantId = tenantId,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
