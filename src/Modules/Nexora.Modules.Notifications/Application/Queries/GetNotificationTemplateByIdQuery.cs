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

/// <summary>Query to get a notification template by ID with translations.</summary>
public sealed record GetNotificationTemplateByIdQuery(Guid Id) : IQuery<NotificationTemplateDetailDto>;

/// <summary>Returns a notification template detail with translations.</summary>
public sealed class GetNotificationTemplateByIdHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetNotificationTemplateByIdHandler> logger) : IQueryHandler<GetNotificationTemplateByIdQuery, NotificationTemplateDetailDto>
{
    public async Task<Result<NotificationTemplateDetailDto>> Handle(
        GetNotificationTemplateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);
        var templateId = NotificationTemplateId.From(request.Id);

        var template = await dbContext.NotificationTemplates
            .Include(t => t.Translations)
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken);

        if (template is null)
        {
            logger.LogDebug("Template {TemplateId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<NotificationTemplateDetailDto>.Failure(
                LocalizedMessage.Of("lockey_notifications_error_template_not_found"));
        }

        var translations = template.Translations
            .Select(tr => new NotificationTemplateTranslationDto(
                tr.Id.Value, tr.LanguageCode, tr.Subject, tr.Body))
            .ToList();

        var dto = new NotificationTemplateDetailDto(
            template.Id.Value, template.Code, template.Module,
            template.Channel.ToString(), template.Subject, template.Body,
            template.Format.ToString(), template.IsSystem, template.IsActive,
            template.CreatedAt, translations);

        return Result<NotificationTemplateDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_notifications_template_retrieved"));
    }
}
