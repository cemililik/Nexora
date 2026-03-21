using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles SignatureCompletedEvent and publishes integration event.</summary>
public sealed class SignatureCompletedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ILogger<SignatureCompletedDomainEventHandler> logger) : INotificationHandler<SignatureCompletedEvent>
{
    /// <summary>
    /// Handles a <see cref="SignatureCompletedEvent"/> by publishing a <see cref="SignatureCompletedIntegrationEvent"/> to the event bus.
    /// Reads TenantId from the signature request entity (same transaction scope as the domain event).
    /// </summary>
    public async Task Handle(SignatureCompletedEvent notification, CancellationToken cancellationToken)
    {
        var request = await dbContext.SignatureRequests
            .FirstOrDefaultAsync(r => r.Id == notification.RequestId, cancellationToken);

        if (request is null)
        {
            logger.LogWarning("SignatureRequest {RequestId} not found for SignatureCompletedEvent, skipping integration event",
                notification.RequestId.Value);
            return;
        }

        var integrationEvent = new SignatureCompletedIntegrationEvent
        {
            TenantId = request.TenantId.ToString(),
            SignatureRequestId = notification.RequestId.Value,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
