using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles SignatureCompletedEvent and publishes integration event.</summary>
public sealed class SignatureCompletedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SignatureCompletedDomainEventHandler> logger) : INotificationHandler<SignatureCompletedEvent>
{
    /// <summary>
    /// Handles a <see cref="SignatureCompletedEvent"/> by publishing a <see cref="SignatureCompletedIntegrationEvent"/> to the event bus.
    /// </summary>
    public async Task Handle(SignatureCompletedEvent notification, CancellationToken cancellationToken)
    {
        var tenantContext = tenantContextAccessor.TryGetCurrent();
        if (tenantContext is null)
        {
            logger.LogWarning("Tenant context unavailable when handling SignatureCompletedEvent for request {RequestId}",
                notification.RequestId.Value);
            return;
        }

        var integrationEvent = new SignatureCompletedIntegrationEvent
        {
            TenantId = tenantContext.TenantId,
            SignatureRequestId = notification.RequestId.Value,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAndLogAsync(integrationEvent, logger, cancellationToken);
    }
}
