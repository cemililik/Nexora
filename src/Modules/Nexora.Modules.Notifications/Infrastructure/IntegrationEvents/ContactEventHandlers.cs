using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles ConsentChangedIntegrationEvent from the Contacts module.
/// When a contact revokes consent, cancels any pending scheduled notifications for that contact.
/// </summary>
public sealed class ConsentChangedIntegrationEventHandler(
    NotificationsDbContext dbContext,
    ILogger<ConsentChangedIntegrationEventHandler> logger) : IIntegrationEventHandler<ConsentChangedIntegrationEvent>
{
    public async Task HandleAsync(ConsentChangedIntegrationEvent @event, CancellationToken ct)
    {
        if (@event.Granted)
        {
            logger.LogDebug("Consent granted for contact {ContactId}, type {ConsentType} — no action needed",
                @event.ContactId, @event.ConsentType);
            return;
        }

        var tenantId = Guid.Parse(@event.TenantId);

        // Find pending scheduled notifications for this contact
        var pendingSchedules = await (from s in dbContext.NotificationSchedules
                                      join n in dbContext.Notifications on s.NotificationId equals n.Id
                                      join r in dbContext.NotificationRecipients
                                          on n.Id equals r.NotificationId
                                      where n.TenantId == tenantId
                                            && r.ContactId == @event.ContactId
                                            && s.Status == ScheduleStatus.Pending
                                      select s)
            .ToListAsync(ct);

        if (pendingSchedules.Count == 0)
        {
            logger.LogDebug("No pending scheduled notifications found for contact {ContactId} in tenant {TenantId}",
                @event.ContactId, tenantId);
            return;
        }

        foreach (var schedule in pendingSchedules)
        {
            schedule.Cancel();
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Cancelled {Count} pending scheduled notifications for contact {ContactId} due to consent revocation ({ConsentType})",
            pendingSchedules.Count, @event.ContactId, @event.ConsentType);
    }
}
