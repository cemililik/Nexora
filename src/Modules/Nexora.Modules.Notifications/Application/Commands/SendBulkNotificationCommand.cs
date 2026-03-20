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

/// <summary>Command to send a bulk notification to multiple recipients.</summary>
public sealed record SendBulkNotificationCommand(
    string Channel,
    IReadOnlyList<BulkRecipient> Recipients,
    string? TemplateCode = null,
    string? Subject = null,
    string? Body = null,
    Dictionary<string, string>? Variables = null,
    string? LanguageCode = null) : ICommand<BulkNotificationResultDto>;

/// <summary>A recipient in a bulk notification.</summary>
public sealed record BulkRecipient(Guid ContactId, string Address);

/// <summary>Validates bulk notification input.</summary>
public sealed class SendBulkNotificationValidator : AbstractValidator<SendBulkNotificationCommand>
{
    public SendBulkNotificationValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("lockey_notifications_validation_send_channel_required")
            .Must(BeValidChannel).WithMessage("lockey_notifications_validation_send_channel_invalid");

        RuleFor(x => x.Recipients)
            .NotEmpty().WithMessage("lockey_notifications_validation_bulk_recipients_required")
            .Must(r => r.Count <= 10000).WithMessage("lockey_notifications_validation_bulk_recipients_max");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.TemplateCode) || (!string.IsNullOrWhiteSpace(x.Subject) && !string.IsNullOrWhiteSpace(x.Body)))
            .WithMessage("lockey_notifications_validation_send_template_or_content_required");
    }

    private static bool BeValidChannel(string channel) =>
        Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out _);
}

/// <summary>Creates a bulk notification with multiple recipients.</summary>
public sealed class SendBulkNotificationHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<SendBulkNotificationHandler> logger) : ICommandHandler<SendBulkNotificationCommand, BulkNotificationResultDto>
{
    public async Task<Result<BulkNotificationResultDto>> Handle(
        SendBulkNotificationCommand request,
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
                logger.LogWarning("Template {TemplateCode} not found for bulk send in tenant {TenantId}", code, tenantId);
                return Result<BulkNotificationResultDto>.Failure(LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
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
            tenantId, channel, subject, body, TriggerSource.BulkApi,
            templateId, triggeredByUserId: null, organizationId: orgId);

        var skipped = 0;
        foreach (var recipient in request.Recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient.Address))
            {
                skipped++;
                continue;
            }
            notification.AddRecipient(recipient.ContactId, recipient.Address);
        }

        if (notification.TotalRecipients == 0)
        {
            logger.LogWarning("Bulk notification has no valid recipients for tenant {TenantId}", tenantId);
            return Result<BulkNotificationResultDto>.Failure(LocalizedMessage.Of("lockey_notifications_error_bulk_no_valid_recipients"));
        }

        await dbContext.Notifications.AddAsync(notification, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new BulkNotificationResultDto(
            notification.Id.Value, request.Recipients.Count,
            notification.TotalRecipients, skipped);

        logger.LogInformation("Bulk notification {NotificationId} queued with {RecipientCount} recipients for tenant {TenantId}",
            notification.Id, notification.TotalRecipients, tenantId);

        return Result<BulkNotificationResultDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_bulk_notification_queued"));
    }
}
