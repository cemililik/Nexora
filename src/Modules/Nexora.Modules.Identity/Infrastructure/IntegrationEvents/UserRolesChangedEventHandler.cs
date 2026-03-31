using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Infrastructure.IntegrationEvents;

/// <summary>
/// Self-consuming handler that invalidates the permission cache when a user's roles change.
/// Ensures cross-instance cache consistency via the integration event bus.
/// </summary>
public sealed class UserRolesChangedEventHandler(
    IdentityDbContext dbContext,
    IInboxGuard inboxGuard,
    ICacheService cacheService,
    ILogger<UserRolesChangedEventHandler> logger) : IIntegrationEventHandler<UserRolesChangedIntegrationEvent>
{
    /// <inheritdoc />
    public async Task HandleAsync(UserRolesChangedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug("Skipping duplicate event {EventId} of type {EventType}", @event.EventId, @event.GetType().Name);
            return;
        }

        await cacheService.RemoveAsync($"auth:permissions:{@event.KeycloakUserId}", ct);

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Permission cache invalidated for user {KeycloakUserId} due to role change {ChangeType}",
            @event.KeycloakUserId, @event.ChangeType);
    }
}
