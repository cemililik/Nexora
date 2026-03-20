using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Queries;

/// <summary>Query to get a notification by ID with recipients.</summary>
public sealed record GetNotificationByIdQuery(Guid Id) : IQuery<NotificationDetailDto>;

/// <summary>Returns a notification detail with recipients.</summary>
public sealed class GetNotificationByIdHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetNotificationByIdHandler> logger) : IQueryHandler<GetNotificationByIdQuery, NotificationDetailDto>
{
    public async Task<Result<NotificationDetailDto>> Handle(
        GetNotificationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var notificationId = NotificationId.From(request.Id);

        var notification = await dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, cancellationToken);

        if (notification is null)
        {
            logger.LogDebug("Notification {NotificationId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<NotificationDetailDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_notification_not_found"));
        }

        var recipients = notification.Recipients
            .Select(r => new NotificationRecipientDto(
                r.Id.Value, r.ContactId, r.RecipientAddress, r.Status.ToString(),
                r.ProviderMessageId, r.FailureReason, r.SentAt, r.DeliveredAt, r.OpenedAt))
            .ToList();

        var dto = new NotificationDetailDto(
            notification.Id.Value, notification.TemplateId?.Value,
            notification.Channel.ToString(), notification.Subject, notification.BodyRendered,
            notification.Status.ToString(), notification.TriggeredBy, notification.TriggeredByUserId,
            notification.TotalRecipients, notification.DeliveredCount, notification.FailedCount,
            notification.OpenedCount, notification.ClickedCount,
            notification.QueuedAt, notification.SentAt, recipients);

        return Result<NotificationDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_notification_retrieved"));
    }
}
