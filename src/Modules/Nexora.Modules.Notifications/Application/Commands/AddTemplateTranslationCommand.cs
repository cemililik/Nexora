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

/// <summary>Command to add or update a template translation.</summary>
public sealed record AddTemplateTranslationCommand(
    Guid TemplateId,
    string LanguageCode,
    string Subject,
    string Body) : ICommand<NotificationTemplateTranslationDto>;

/// <summary>Validates template translation input.</summary>
public sealed class AddTemplateTranslationValidator : AbstractValidator<AddTemplateTranslationCommand>
{
    public AddTemplateTranslationValidator()
    {
        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_id_required");

        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("lockey_notifications_validation_translation_language_required")
            .MaximumLength(10).WithMessage("lockey_notifications_validation_translation_language_max_length");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_subject_required")
            .MaximumLength(500).WithMessage("lockey_notifications_validation_template_subject_max_length");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_body_required");
    }
}

/// <summary>Adds a new translation or updates an existing one for a notification template.</summary>
public sealed class AddTemplateTranslationHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddTemplateTranslationHandler> logger) : ICommandHandler<AddTemplateTranslationCommand, NotificationTemplateTranslationDto>
{
    public async Task<Result<NotificationTemplateTranslationDto>> Handle(
        AddTemplateTranslationCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var templateId = NotificationTemplateId.From(request.TemplateId);

        var template = await dbContext.NotificationTemplates
            .Include(t => t.Translations)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found for translation in tenant {TenantId}",
                request.TemplateId, tenantId);
            return Result<NotificationTemplateTranslationDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
        }

        var langCode = request.LanguageCode.Trim().ToLowerInvariant();
        var existingTranslation = template.Translations
            .FirstOrDefault(t => t.LanguageCode.Equals(langCode, StringComparison.OrdinalIgnoreCase));

        if (existingTranslation is not null)
        {
            template.UpdateTranslation(langCode, request.Subject, request.Body);

            var updatedDto = new NotificationTemplateTranslationDto(
                existingTranslation.Id.Value, existingTranslation.LanguageCode,
                request.Subject.Trim(), request.Body);

            logger.LogInformation("Translation {LanguageCode} updated for template {TemplateId}",
                langCode, template.Id);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result<NotificationTemplateTranslationDto>.Success(updatedDto,
                LocalizedMessage.Of("lockey_notifications_translation_updated"));
        }

        template.AddTranslation(langCode, request.Subject, request.Body);
        await dbContext.SaveChangesAsync(cancellationToken);

        var newTranslation = template.Translations.First(t =>
            t.LanguageCode.Equals(langCode, StringComparison.OrdinalIgnoreCase));

        var dto = new NotificationTemplateTranslationDto(
            newTranslation.Id.Value, newTranslation.LanguageCode,
            newTranslation.Subject, newTranslation.Body);

        logger.LogInformation("Translation {LanguageCode} added to template {TemplateId}",
            langCode, template.Id);

        return Result<NotificationTemplateTranslationDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_translation_added"));
    }
}
