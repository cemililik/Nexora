using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.Services;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to schedule a notification for future delivery.</summary>
public sealed record ScheduleNotificationCommand(
    string Channel,
    Guid ContactId,
    string RecipientAddress,
    DateTime ScheduledAt,
    string? TemplateCode = null,
    string? Subject = null,
    string? Body = null,
    Dictionary<string, string>? Variables = null,
    string? LanguageCode = null) : ICommand<NotificationScheduleDto>;

/// <summary>Validates schedule notification input.</summary>
public sealed class ScheduleNotificationValidator : AbstractValidator<ScheduleNotificationCommand>
{
    public ScheduleNotificationValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_channel_required")
            .Must(BeValidChannel).WithMessage("lockey_notifications_validation_send_channel_invalid");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_contact_required");

        RuleFor(x => x.RecipientAddress)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_address_required");

        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTime.UtcNow).WithMessage("lockey_notifications_validation_schedule_must_be_future");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.TemplateCode) || (!string.IsNullOrWhiteSpace(x.Subject) && !string.IsNullOrWhiteSpace(x.Body)))
            .WithMessage("lockey_notifications_validation_send_template_or_content_required");
    }

    private static bool BeValidChannel(string channel) =>
        Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out _);
}

/// <summary>Creates a scheduled notification for future dispatch.</summary>
public sealed class ScheduleNotificationHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<ScheduleNotificationHandler> logger) : ICommandHandler<ScheduleNotificationCommand, NotificationScheduleDto>
{
    public async Task<Result<NotificationScheduleDto>> Handle(
        ScheduleNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var channel = Enum.Parse<NotificationChannel>(request.Channel, ignoreCase: true);
        var orgId = tenantContextAccessor.Current.OrganizationId is { } orgStr
            ? Guid.Parse(orgStr) : (Guid?)null;

        string subject;
        string body;
        NotificationTemplateId? templateId = null;

        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            var code = request.TemplateCode.Trim().ToLowerInvariant();
            var template = await dbContext.NotificationTemplates
                .Include(t => t.Translations)
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Code == code && t.Channel == channel && t.IsActive,
                    cancellationToken);

            if (template is null)
            {
                logger.LogWarning("Template {TemplateCode} not found for scheduling in tenant {TenantId}", code, tenantId);
                return Result<NotificationScheduleDto>.Failure(LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
            }

            templateId = template.Id;
            var (s, b) = TemplateRenderer.Render(template, request.Variables ?? new(), request.LanguageCode);
            subject = s;
            body = b;
        }
        else
        {
            subject = request.Subject!;
            body = TemplateRenderer.RenderInline(request.Body!, request.Variables ?? new());
        }

        var notification = Notification.Create(
            tenantId, channel, subject, body, "scheduled",
            templateId, triggeredByUserId: null, organizationId: orgId);
        notification.AddRecipient(request.ContactId, request.RecipientAddress);

        var schedule = NotificationSchedule.Create(notification.Id, request.ScheduledAt);

        await dbContext.Notifications.AddAsync(notification, cancellationToken);
        await dbContext.NotificationSchedules.AddAsync(schedule, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationScheduleDto(
            schedule.Id.Value, notification.Id.Value,
            schedule.ScheduledAt, schedule.Status.ToString(),
            schedule.CreatedAt);

        logger.LogInformation("Notification {NotificationId} scheduled for {ScheduledAt} in tenant {TenantId}",
            notification.Id, request.ScheduledAt, tenantId);

        return Result<NotificationScheduleDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_notification_scheduled"));
    }
}
