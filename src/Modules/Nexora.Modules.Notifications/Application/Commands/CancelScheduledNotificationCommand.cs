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

/// <summary>Command to cancel a scheduled notification.</summary>
public sealed record CancelScheduledNotificationCommand(Guid ScheduleId) : ICommand<object>;

/// <summary>Validates cancel schedule input.</summary>
public sealed class CancelScheduledNotificationValidator : AbstractValidator<CancelScheduledNotificationCommand>
{
    public CancelScheduledNotificationValidator()
    {
        RuleFor(x => x.ScheduleId)
            .NotEmpty().WithMessage("lockey_notifications_validation_schedule_id_required");
    }
}

/// <summary>Cancels a pending scheduled notification.</summary>
public sealed class CancelScheduledNotificationHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CancelScheduledNotificationHandler> logger) : ICommandHandler<CancelScheduledNotificationCommand, object>
{
    public async Task<Result<object>> Handle(
        CancelScheduledNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var scheduleId = NotificationScheduleId.From(request.ScheduleId);

        var schedule = await dbContext.NotificationSchedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);

        if (schedule is null)
        {
            logger.LogWarning("Schedule {ScheduleId} not found", request.ScheduleId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_schedule_not_found"));
        }

        // Verify the notification belongs to this tenant
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == schedule.NotificationId && n.TenantId == tenantId, cancellationToken);

        if (notification is null)
        {
            logger.LogWarning("Notification for schedule {ScheduleId} not found in tenant {TenantId}",
                request.ScheduleId, tenantId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_schedule_not_found"));
        }

        schedule.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Schedule {ScheduleId} cancelled for tenant {TenantId}", schedule.Id, tenantId);

        return Result<object>.Success(null!,
            LocalizedMessage.Of("lockey_notifications_schedule_cancelled"));
    }
}
