using MediatR;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.Commands;
using Nexora.SharedKernel.Abstractions.Modules;

namespace Nexora.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Implementation of <see cref="INotificationService"/> for cross-module notification sending.
/// </summary>
public sealed class NotificationService(
    ISender sender,
    ILogger<NotificationService> logger) : INotificationService
{
    /// <inheritdoc />
    public async Task<Guid> SendAsync(SendNotificationRequest request, CancellationToken ct = default)
    {
        var command = new SendNotificationCommand(
            request.Channel,
            request.ContactId,
            request.RecipientAddress,
            TemplateCode: request.TemplateCode,
            Variables: request.Variables);

        var result = await sender.Send(command, ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("Notification {NotificationId} sent via {Channel} to contact {ContactId}",
                result.Value!.Id, request.Channel, request.ContactId);
            return result.Value!.Id;
        }

        logger.LogWarning("Failed to send notification via {Channel} to contact {ContactId}",
            request.Channel, request.ContactId);
        return Guid.Empty;
    }

    /// <inheritdoc />
    public async Task<Guid> SendBulkAsync(SendBulkNotificationRequest request, CancellationToken ct = default)
    {
        var recipients = request.Recipients
            .Select(r => new BulkRecipient(r.ContactId, r.Address))
            .ToList();

        var command = new SendBulkNotificationCommand(
            request.Channel,
            recipients,
            TemplateCode: request.TemplateCode,
            Variables: request.Variables);

        var result = await sender.Send(command, ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("Bulk notification {NotificationId} queued via {Channel} for {RecipientCount} contacts",
                result.Value!.NotificationId, request.Channel, request.Recipients.Count);
            return result.Value!.NotificationId;
        }

        logger.LogWarning("Failed to send bulk notification via {Channel} for {RecipientCount} contacts",
            request.Channel, request.Recipients.Count);
        return Guid.Empty;
    }

    /// <inheritdoc />
    public async Task<Guid> ScheduleAsync(ScheduleNotificationRequest request, CancellationToken ct = default)
    {
        var command = new ScheduleNotificationCommand(
            request.Channel,
            request.ContactId,
            request.RecipientAddress,
            request.ScheduledAt,
            TemplateCode: request.TemplateCode,
            Variables: request.Variables);

        var result = await sender.Send(command, ct);

        if (result.IsSuccess)
        {
            logger.LogInformation("Notification scheduled {ScheduleId} via {Channel} for contact {ContactId} at {ScheduledAt}",
                result.Value!.Id, request.Channel, request.ContactId, request.ScheduledAt);
            return result.Value!.Id;
        }

        logger.LogWarning("Failed to schedule notification via {Channel} for contact {ContactId}",
            request.Channel, request.ContactId);
        return Guid.Empty;
    }
}
