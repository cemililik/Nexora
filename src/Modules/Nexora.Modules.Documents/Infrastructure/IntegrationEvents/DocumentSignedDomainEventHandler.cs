using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentSignedEvent and publishes integration event.</summary>
public sealed class DocumentSignedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ILogger<DocumentSignedDomainEventHandler> logger) : INotificationHandler<DocumentSignedEvent>
{
    /// <summary>
    /// Handles a <see cref="DocumentSignedEvent"/> by publishing a <see cref="DocumentSignedIntegrationEvent"/> to the event bus.
    /// Logs a warning and skips if the signature request or recipient is not found.
    /// </summary>
    public async Task Handle(DocumentSignedEvent notification, CancellationToken cancellationToken)
    {
        var request = await dbContext.SignatureRequests
            .FirstOrDefaultAsync(r => r.Id == notification.RequestId, cancellationToken);

        var recipient = await dbContext.SignatureRecipients
            .FirstOrDefaultAsync(r => r.Id == notification.RecipientId && r.RequestId == notification.RequestId, cancellationToken);

        if (request is null)
        {
            logger.LogWarning("SignatureRequest {RequestId} not found for DocumentSignedEvent", notification.RequestId);
            return;
        }

        if (recipient is null)
        {
            logger.LogWarning("SignatureRecipient {RecipientId} not found for DocumentSignedEvent", notification.RecipientId);
            return;
        }

        var integrationEvent = new DocumentSignedIntegrationEvent
        {
            TenantId = request.TenantId.ToString(),
            SignatureRequestId = notification.RequestId.Value,
            DocumentId = request.DocumentId.Value,
            RecipientContactId = recipient.ContactId
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
