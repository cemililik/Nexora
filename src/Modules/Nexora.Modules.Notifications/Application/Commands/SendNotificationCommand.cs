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

/// <summary>Command to send a notification to a single recipient.</summary>
public sealed record SendNotificationCommand(
    string Channel,
    Guid ContactId,
    string RecipientAddress,
    string? TemplateCode = null,
    string? Subject = null,
    string? Body = null,
    Dictionary<string, string>? Variables = null,
    string? LanguageCode = null) : ICommand<NotificationDto>;

/// <summary>Validates send notification input.</summary>
public sealed class SendNotificationValidator : AbstractValidator<SendNotificationCommand>
{
    public SendNotificationValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_channel_required")
            .Must(BeValidChannel).WithMessage("lockey_notifications_validation_send_channel_invalid");

        RuleFor(x => x.ContactId)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_contact_required");

        RuleFor(x => x.RecipientAddress)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_address_required")
            .MaximumLength(256).WithMessage("lockey_notifications_validation_send_address_max_length");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.TemplateCode) || (!string.IsNullOrWhiteSpace(x.Subject) && !string.IsNullOrWhiteSpace(x.Body)))
            .WithMessage("lockey_notifications_validation_send_template_or_content_required");
    }

    private static bool BeValidChannel(string channel) =>
        Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out _);
}

/// <summary>Creates a notification record and queues it for delivery.</summary>
public sealed class SendNotificationHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SendNotificationHandler> logger) : ICommandHandler<SendNotificationCommand, NotificationDto>
{
    public async Task<Result<NotificationDto>> Handle(
        SendNotificationCommand request,
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
                logger.LogWarning("Template {TemplateCode} not found or inactive for channel {Channel} in tenant {TenantId}",
                    code, channel, tenantId);
                return Result<NotificationDto>.Failure(
                    LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
            }

            templateId = template.Id;
            var (resolvedSubject, resolvedBody) = TemplateRenderer.Render(
                template, request.Variables ?? new(), request.LanguageCode);
            subject = resolvedSubject;
            body = resolvedBody;
        }
        else
        {
            var vars = request.Variables ?? new();
            subject = TemplateRenderer.RenderInline(request.Subject!, vars, htmlEncode: false)
                .Replace("\r", string.Empty).Replace("\n", string.Empty);
            body = TemplateRenderer.RenderInline(request.Body!, vars, htmlEncode: false);
        }

        var notification = Notification.Create(
            tenantId, channel, subject, body, TriggerSource.Api,
            templateId, triggeredByUserId: null, organizationId: orgId);

        notification.AddRecipient(request.ContactId, request.RecipientAddress);
        notification.MarkSending();

        await dbContext.Notifications.AddAsync(notification, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationDto(
            notification.Id.Value, notification.Channel.ToString(), notification.Subject,
            notification.Status.ToString(), notification.TriggeredBy,
            notification.TotalRecipients, notification.DeliveredCount, notification.FailedCount,
            notification.QueuedAt, notification.SentAt);

        logger.LogInformation("Notification {NotificationId} queued for {Channel} to contact {ContactId} in tenant {TenantId}",
            notification.Id, channel, request.ContactId, tenantId);

        return Result<NotificationDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_notification_sent"));
    }
}
