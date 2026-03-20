using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentArchivedEvent and publishes integration event.</summary>
public sealed class DocumentArchivedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DocumentArchivedDomainEventHandler> logger) : INotificationHandler<DocumentArchivedEvent>
{
    /// <summary>
    /// Handles a <see cref="DocumentArchivedEvent"/> by publishing a <see cref="DocumentArchivedIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(DocumentArchivedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling DocumentArchivedEvent for document {DocumentId}",
                notification.DocumentId.Value);
            return;
        }

        var integrationEvent = new DocumentArchivedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
