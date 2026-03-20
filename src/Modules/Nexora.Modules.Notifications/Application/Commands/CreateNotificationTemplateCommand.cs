using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.Entities;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Commands;

/// <summary>Command to create a new notification template.</summary>
public sealed record CreateNotificationTemplateCommand(
    string Code,
    string Module,
    string Channel,
    string Subject,
    string Body,
    string Format,
    bool IsSystem = false) : ICommand<NotificationTemplateDto>;

/// <summary>Validates notification template creation input.</summary>
public sealed class CreateNotificationTemplateValidator : AbstractValidator<CreateNotificationTemplateCommand>
{
    public CreateNotificationTemplateValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_code_required")
            .MaximumLength(100).WithMessage("lockey_notifications_validation_template_code_max_length")
            .Matches("^[a-z0-9_.-]+$").WithMessage("lockey_notifications_validation_template_code_format");

        RuleFor(x => x.Module)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_module_required")
            .MaximumLength(50).WithMessage("lockey_notifications_validation_template_module_max_length");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_channel_required")
            .Must(BeValidChannel).WithMessage("lockey_notifications_validation_template_channel_invalid");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_subject_required")
            .MaximumLength(500).WithMessage("lockey_notifications_validation_template_subject_max_length");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_body_required");

        RuleFor(x => x.Format)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_format_required")
            .Must(BeValidFormat).WithMessage("lockey_notifications_validation_template_format_invalid");
    }

    private static bool BeValidChannel(string channel) =>
        Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out _);

    private static bool BeValidFormat(string format) =>
        Enum.TryParse<TemplateFormat>(format, ignoreCase: true, out _);
}

/// <summary>Creates a notification template and persists it to the database.</summary>
public sealed class CreateNotificationTemplateHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateNotificationTemplateHandler> logger) : ICommandHandler<CreateNotificationTemplateCommand, NotificationTemplateDto>
{
    public async Task<Result<NotificationTemplateDto>> Handle(
        CreateNotificationTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var channel = Enum.Parse<NotificationChannel>(request.Channel, ignoreCase: true);
        var format = Enum.Parse<TemplateFormat>(request.Format, ignoreCase: true);
        var code = request.Code.Trim().ToLowerInvariant();

        var exists = await dbContext.NotificationTemplates
            .AnyAsync(t => t.TenantId == tenantId && t.Code == code && t.Channel == channel, cancellationToken);

        if (exists)
        {
            logger.LogWarning("Template creation failed: code {Code} already exists for channel {Channel} in tenant {TenantId}",
                code, channel, tenantId);
            return Result<NotificationTemplateDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_template_code_exists",
                new Dictionary<string, string> { ["code"] = code, ["channel"] = request.Channel }));
        }

        var orgId = tenantContextAccessor.Current.OrganizationId is { } orgStr
            ? Guid.Parse(orgStr) : (Guid?)null;

        var template = NotificationTemplate.Create(
            tenantId, code, request.Module, channel,
            request.Subject, request.Body, format, request.IsSystem, orgId);

        await dbContext.NotificationTemplates.AddAsync(template, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = new NotificationTemplateDto(
            template.Id.Value, template.Code, template.Module,
            template.Channel.ToString(), template.Subject,
            template.Format.ToString(), template.IsSystem, template.IsActive,
            template.CreatedAt);

        logger.LogInformation("Template {TemplateId} created with code {Code} for tenant {TenantId}",
            template.Id, template.Code, tenantId);

        return Result<NotificationTemplateDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_template_created"));
    }
}
