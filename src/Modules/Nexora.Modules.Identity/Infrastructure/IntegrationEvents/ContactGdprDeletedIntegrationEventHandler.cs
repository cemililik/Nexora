using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Identity.Infrastructure.IntegrationEvents;

/// <summary>
/// Consumes <see cref="ContactGdprDeletedIntegrationEvent"/> from the Contacts module.
/// Nulls out <c>User.ContactId</c> on every user in the target tenant previously linked to the erased contact,
/// and emits a <see cref="UserContactUnlinkedIntegrationEvent"/> per user with <c>Reason = "gdpr_erasure"</c>.
/// Idempotent via <see cref="IInboxGuard"/>.
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandler(
    IdentityDbContext dbContext,
    IInboxGuard inboxGuard,
    IOutbox outbox,
    ILogger<ContactGdprDeletedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<ContactGdprDeletedIntegrationEvent>
{
    /// <inheritdoc />
    public async Task HandleAsync(ContactGdprDeletedIntegrationEvent @event, CancellationToken ct)
    {
        if (await inboxGuard.IsAlreadyProcessedAsync(@event.EventId, ct))
        {
            logger.LogDebug(
                "Skipping duplicate event {EventId} of type {EventType}",
                @event.EventId, @event.GetType().Name);
            return;
        }

        // Scope strictly to the tenant that emitted the event — cross-tenant users MUST NOT be touched.
        TenantId? tenantId = null;
        if (Guid.TryParse(@event.TenantId, out var tenantGuid))
            tenantId = TenantId.From(tenantGuid);

        var linkedUsers = tenantId is null
            ? new List<Domain.Entities.User>()
            : await dbContext.Users
                .Where(u => u.TenantId == tenantId && u.ContactId == @event.ContactId)
                .ToListAsync(ct);

        var unlinkedAt = DateTime.UtcNow;

        foreach (var user in linkedUsers)
        {
            user.UnlinkContact();

            await outbox.EnqueueAsync(new UserContactUnlinkedIntegrationEvent
            {
                TenantId = @event.TenantId,
                UserId = user.Id.Value,
                UnlinkedAtUtc = unlinkedAt,
                Reason = "gdpr_erasure"
            }, ct);
        }

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "GDPR erasure for contact {ContactId} in tenant {TenantId}: unlinked {Count} user(s)",
            @event.ContactId, @event.TenantId, linkedUsers.Count);
    }
}
