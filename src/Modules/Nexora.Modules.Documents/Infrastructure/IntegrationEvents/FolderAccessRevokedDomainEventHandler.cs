using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Documents.Domain.Events;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Documents.Infrastructure.IntegrationEvents;

/// <summary>Handles FolderAccessRevokedEvent and enqueues integration event to the outbox.</summary>
public sealed class FolderAccessRevokedDomainEventHandler(
    IOutbox outbox,
    DocumentsDbContext dbContext,
    ILogger<FolderAccessRevokedDomainEventHandler> logger) : INotificationHandler<FolderAccessRevokedEvent>
{
    /// <inheritdoc />
    public async Task Handle(FolderAccessRevokedEvent notification, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == notification.FolderId, cancellationToken);

        if (folder is null)
        {
            logger.LogWarning("Folder {FolderId} not found for domain event, skipping integration event", notification.FolderId);
            return;
        }

        var integrationEvent = new FolderAccessRevokedIntegrationEvent
        {
            TenantId = folder.TenantId.ToString(),
            FolderId = folder.Id.Value,
            AccessId = notification.AccessId.Value
        };

        await outbox.EnqueueAsync(integrationEvent, cancellationToken);

        logger.LogInformation("Enqueued {EventType} for folder {FolderId} in tenant {TenantId}",
            nameof(FolderAccessRevokedIntegrationEvent), folder.Id, integrationEvent.TenantId);
    }
}
