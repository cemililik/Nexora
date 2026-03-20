using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.MultiTenancy;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles DocumentCreatedEvent and publishes integration event.</summary>
public sealed class DocumentCreatedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ILogger<DocumentCreatedDomainEventHandler> logger) : INotificationHandler<DocumentCreatedEvent>
{
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

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published DocumentUploadedIntegrationEvent for {DocumentId}", document.Id);
    }
}

/// <summary>Handles DocumentArchivedEvent and publishes integration event.</summary>
public sealed class DocumentArchivedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DocumentArchivedDomainEventHandler> logger) : INotificationHandler<DocumentArchivedEvent>
{
    public async Task Handle(DocumentArchivedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new DocumentArchivedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published DocumentArchivedIntegrationEvent for {DocumentId}", notification.DocumentId);
    }
}

/// <summary>Handles DocumentSignedEvent and publishes integration event.</summary>
public sealed class DocumentSignedDomainEventHandler(
    IEventBus eventBus,
    DocumentsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DocumentSignedDomainEventHandler> logger) : INotificationHandler<DocumentSignedEvent>
{
    public async Task Handle(DocumentSignedEvent notification, CancellationToken cancellationToken)
    {
        var request = await dbContext.SignatureRequests
            .FirstOrDefaultAsync(r => r.Id == notification.RequestId, cancellationToken);

        var recipient = await dbContext.SignatureRecipients
            .FirstOrDefaultAsync(r => r.Id == notification.RecipientId, cancellationToken);

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
            TenantId = tenantContextAccessor.Current.TenantId,
            SignatureRequestId = notification.RequestId.Value,
            DocumentId = request.DocumentId.Value,
            RecipientContactId = recipient.ContactId
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published DocumentSignedIntegrationEvent for request {RequestId} recipient {RecipientId}",
            notification.RequestId, notification.RecipientId);
    }
}

/// <summary>Handles SignatureCompletedEvent and publishes integration event.</summary>
public sealed class SignatureCompletedDomainEventHandler(
    IEventBus eventBus,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SignatureCompletedDomainEventHandler> logger) : INotificationHandler<SignatureCompletedEvent>
{
    public async Task Handle(SignatureCompletedEvent notification, CancellationToken cancellationToken)
    {
        var integrationEvent = new SignatureCompletedIntegrationEvent
        {
            TenantId = tenantContextAccessor.Current.TenantId,
            SignatureRequestId = notification.RequestId.Value,
            DocumentId = notification.DocumentId.Value
        };

        await eventBus.PublishAsync(integrationEvent, cancellationToken);
        logger.LogInformation("Published SignatureCompletedIntegrationEvent for request {RequestId}",
            notification.RequestId);
    }
}
