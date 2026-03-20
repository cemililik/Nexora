using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to update an existing notification template.</summary>
public sealed record UpdateNotificationTemplateCommand(
    Guid Id,
    string Subject,
    string Body,
    string Format) : ICommand<NotificationTemplateDto>;

/// <summary>Validates notification template update input.</summary>
public sealed class UpdateNotificationTemplateValidator : AbstractValidator<UpdateNotificationTemplateCommand>
{
    public UpdateNotificationTemplateValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_id_required");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_subject_required")
            .MaximumLength(500).WithMessage("lockey_notifications_validation_template_subject_max_length");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_body_required");

        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_format_required")
            .Must(BeValidFormat).WithMessage("lockey_notifications_validation_template_format_invalid");
    }

    private static bool BeValidFormat(string format) =>
        Enum.TryParse<TemplateFormat>(format, ignoreCase: true, out _);
}

/// <summary>Updates a notification template's subject, body, and format.</summary>
public sealed class UpdateNotificationTemplateHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateNotificationTemplateHandler> logger) : ICommandHandler<UpdateNotificationTemplateCommand, NotificationTemplateDto>
{
    public async Task<Result<NotificationTemplateDto>> Handle(
        UpdateNotificationTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var templateId = NotificationTemplateId.From(request.Id);

        var template = await dbContext.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<NotificationTemplateDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
        }

        var format = Enum.Parse<TemplateFormat>(request.Format, ignoreCase: true);
        template.Update(request.Subject, request.Body, format);

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationTemplateDto(
            template.Id.Value, template.Code, template.Module,
            template.Channel.ToString(), template.Subject,
            template.Format.ToString(), template.IsSystem, template.IsActive,
            template.CreatedAt);

        logger.LogInformation("Template {TemplateId} updated for tenant {TenantId}", template.Id, tenantId);

        return Result<NotificationTemplateDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_template_updated"));
    }
}
