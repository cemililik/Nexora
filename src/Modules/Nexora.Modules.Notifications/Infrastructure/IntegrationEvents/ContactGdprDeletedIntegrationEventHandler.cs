using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Constants;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles <see cref="ContactGdprDeletedIntegrationEvent"/> from the Contacts module.
/// Scrubs PII from Notification aggregates that were sent to the erased contact:
/// redacts every <see cref="Domain.Entities.NotificationRecipient.RecipientAddress"/>
/// for the contact and clears the rendered body on each parent <see cref="Domain.Entities.Notification"/>.
/// Preserves <c>template_key</c>, delivery status, timestamps, and counters — these retain
/// audit value and are not personal data per the Notifications PII retention policy.
/// </summary>
public sealed class ContactGdprDeletedIntegrationEventHandler(
    NotificationsDbContext dbContext,
    IInboxGuard inboxGuard,
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

        if (!Guid.TryParse(@event.TenantId, out var tenantId))
        {
            logger.LogWarning(
                "GDPR erasure event {EventId} has invalid TenantId {TenantId}; marking processed to prevent redelivery loop",
                @event.EventId, @event.TenantId);
            inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
            await dbContext.SaveChangesAsync(ct);
            return;
        }

        int scrubbedRecipientCount;
        int scrubbedNotificationCount;

        if (dbContext.Database.IsRelational())
        {
            // Scope parent-notification updates to the tenant via a subquery over recipients.
            // Execute the notifications update BEFORE the recipient scrub so the subquery
            // still sees the intact recipient rows — ContactId is not touched by the scrub
            // in the current implementation, but ordering is preserved defensively.
            scrubbedNotificationCount = await dbContext.Notifications
                .Where(n => n.TenantId == tenantId
                         && dbContext.NotificationRecipients
                             .Where(r => r.ContactId == @event.ContactId)
                             .Select(r => r.NotificationId)
                             .Contains(n.Id))
                .ExecuteUpdateAsync(setters => setters
                    // T-017: BodyRendered is nullable — write null so auditors can
                    // distinguish an erased row from real content. Subject stays a
                    // placeholder (column remains NOT NULL) to preserve listing UX.
                    .SetProperty(n => n.BodyRendered, (string?)null)
                    .SetProperty(n => n.Subject, PiiRedactedPlaceholder.Value),
                    ct);

            scrubbedRecipientCount = await dbContext.NotificationRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.RecipientAddress, PiiRedactedPlaceholder.Value)
                    .SetProperty(r => r.FailureReason, (string?)null),
                    ct);

            if (scrubbedRecipientCount == 0 && scrubbedNotificationCount == 0)
            {
                inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation(
                    "No Notification recipients found for erased contact {ContactId} in tenant {TenantId}; inbox marked",
                    @event.ContactId, tenantId);
                return;
            }
        }
        else
        {
            // InMemory fallback — load + domain methods so tests continue to pass.
            var recipients = await dbContext.NotificationRecipients
                .Where(r => r.ContactId == @event.ContactId)
                .ToListAsync(ct);

            if (recipients.Count == 0)
            {
                inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation(
                    "No Notification recipients found for erased contact {ContactId} in tenant {TenantId}; inbox marked",
                    @event.ContactId, tenantId);
                return;
            }

            var notificationIds = recipients.Select(r => r.NotificationId).Distinct().ToList();

            var notifications = await dbContext.Notifications
                .Where(n => notificationIds.Contains(n.Id) && n.TenantId == tenantId)
                .ToListAsync(ct);

            var allowedNotificationIds = notifications.Select(n => n.Id).ToHashSet();

            scrubbedRecipientCount = 0;
            foreach (var recipient in recipients)
            {
                if (!allowedNotificationIds.Contains(recipient.NotificationId))
                {
                    continue;
                }

                recipient.ScrubRecipientAddress();
                scrubbedRecipientCount++;
            }

            foreach (var notification in notifications)
            {
                notification.ScrubRenderedBody();
            }

            scrubbedNotificationCount = notifications.Count;
        }

        inboxGuard.MarkAsProcessed(@event.EventId, @event.GetType().Name);
        await dbContext.SaveChangesAsync(ct);

        // Do NOT log @event.Reason — it is free-text user input and can contain PII
        // (names, emails, phone numbers). EventId + ContactId + counts are sufficient for audit.
        logger.LogInformation(
            "GDPR erasure event {EventId} for contact {ContactId} in tenant {TenantId}: scrubbed {RecipientCount} recipients across {NotificationCount} notifications (mode={Mode})",
            @event.EventId, @event.ContactId, tenantId, scrubbedRecipientCount, scrubbedNotificationCount, @event.Mode);
    }
}
