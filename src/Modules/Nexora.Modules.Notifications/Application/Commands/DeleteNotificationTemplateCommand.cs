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

/// <summary>Command to soft-delete (deactivate) a notification template.</summary>
public sealed record DeleteNotificationTemplateCommand(Guid Id) : ICommand<object>;

/// <summary>Validates notification template deletion input.</summary>
public sealed class DeleteNotificationTemplateValidator : AbstractValidator<DeleteNotificationTemplateCommand>
{
    public DeleteNotificationTemplateValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("lockey_notifications_validation_template_id_required");
    }
}

/// <summary>Soft-deletes a notification template by deactivating it.</summary>
public sealed class DeleteNotificationTemplateHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<DeleteNotificationTemplateHandler> logger) : ICommandHandler<DeleteNotificationTemplateCommand, object>
{
    public async Task<Result<object>> Handle(
        DeleteNotificationTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var templateId = NotificationTemplateId.From(request.Id);

        var template = await dbContext.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("Template {TemplateId} not found for deletion in tenant {TenantId}", request.Id, tenantId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
        }

        if (template.IsSystem)
        {
            logger.LogWarning("Cannot delete system template {TemplateId} in tenant {TenantId}", request.Id, tenantId);
            return Result<object>.Failure(LocalizedMessage.Of("lockey_notifications_error_cannot_delete_system_template"));
        }

        template.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Template {TemplateId} deactivated for tenant {TenantId}", template.Id, tenantId);

        return Result<object>.Success(null!,
            LocalizedMessage.Of("lockey_notifications_template_deleted"));
    }
}
