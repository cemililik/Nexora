using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to update delivery status from a webhook callback.</summary>
public sealed record UpdateDeliveryStatusCommand(
    string ProviderMessageId,
    string Status,
    string? FailureReason = null) : ICommand<object>;

/// <summary>Validates delivery status update input.</summary>
public sealed class UpdateDeliveryStatusValidator : AbstractValidator<UpdateDeliveryStatusCommand>
{
    private static readonly string[] ValidStatuses = ["delivered", "opened", "bounced", "failed"];

    public UpdateDeliveryStatusValidator()
    {
        RuleFor(x => x.ProviderMessageId)
            .NotEmpty().WithMessage("lockey_notifications_validation_delivery_provider_message_id_required");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("lockey_notifications_validation_delivery_status_required")
            .Must(s => ValidStatuses.Contains(s.ToLowerInvariant()))
            .WithMessage("lockey_notifications_validation_delivery_status_invalid");
    }
}

/// <summary>Updates a recipient's delivery status based on provider webhook data.</summary>
public sealed class UpdateDeliveryStatusHandler(
    NotificationsDbContext dbContext,
    ILogger<UpdateDeliveryStatusHandler> logger) : ICommandHandler<UpdateDeliveryStatusCommand, object>
{
    public async Task<Result<object>> Handle(
        UpdateDeliveryStatusCommand request,
        CancellationToken cancellationToken)
    {
        // Lookup notification via recipient's ProviderMessageId (webhook callback only has this)
        var notification = await dbContext.Notifications
            .Include(n => n.Recipients)
            .FirstOrDefaultAsync(n => n.Recipients.Any(r => r.ProviderMessageId == request.ProviderMessageId),
                cancellationToken);

        if (notification is null)
        {
            logger.LogWarning("No notification found with provider message {ProviderMessageId}", request.ProviderMessageId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_notification_not_found"));
        }

        var recipient = notification.Recipients
            .First(r => r.ProviderMessageId == request.ProviderMessageId);

        switch (request.Status.ToLowerInvariant())
        {
            case "delivered":
                recipient.MarkDelivered();
                break;
            case "opened":
                recipient.MarkOpened();
                break;
            case "bounced":
                recipient.MarkBounced(request.FailureReason ?? "lockey_notifications_error_unknown_bounce_reason");
                break;
            case "failed":
                recipient.MarkFailed(request.FailureReason ?? "lockey_notifications_error_unknown_failure");
                break;
        }

        // Update notification-level counts
        var delivered = notification.Recipients.Count(r => r.Status == RecipientStatus.Delivered);
        var failed = notification.Recipients.Count(r => r.Status is RecipientStatus.Failed or RecipientStatus.Bounced);
        var opened = notification.Recipients.Count(r => r.Status == RecipientStatus.Opened);
        var clicked = notification.Recipients.Count(r => r.Status == RecipientStatus.Clicked);
        notification.UpdateCounts(delivered, failed, opened, clicked);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Delivery status updated to {Status} for recipient {ProviderMessageId} in notification {NotificationId}",
            request.Status, request.ProviderMessageId, notification.Id);

        return Result<object>.Success(null!,
            LocalizedMessage.Of("lockey_notifications_delivery_status_updated"));
    }
}
