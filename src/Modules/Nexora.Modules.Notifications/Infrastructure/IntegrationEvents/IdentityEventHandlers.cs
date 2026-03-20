using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Messaging;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Domain.Events;

namespace Nexora.Modules.Notifications.Infrastructure.IntegrationEvents;

/// <summary>
/// Handles UserCreatedIntegrationEvent from the Identity module.
/// Sends a welcome notification to newly created users.
/// </summary>
public sealed class UserCreatedIntegrationEventHandler(
    INotificationService notificationService,
    ILogger<UserCreatedIntegrationEventHandler> logger) : IIntegrationEventHandler<UserCreatedIntegrationEvent>
{
    public async Task HandleAsync(UserCreatedIntegrationEvent @event, CancellationToken ct)
    {
        var request = new SendNotificationRequest(
            TemplateCode: "welcome",
            Channel: "Email",
            ContactId: @event.UserId,
            Variables: new Dictionary<string, string>
            {
                ["email"] = @event.Email
            });

        var notificationId = await notificationService.SendAsync(request, ct);

        if (notificationId != Guid.Empty)
        {
            logger.LogInformation("Welcome notification {NotificationId} sent to user {UserId}",
                notificationId, @event.UserId);
        }
        else
        {
            logger.LogWarning("Failed to send welcome notification to user {UserId}. " +
                              "Template 'welcome' may not be configured for the tenant",
                @event.UserId);
        }
    }
}
