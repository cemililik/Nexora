using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Queries;

/// <summary>Paginated query to list notification templates with optional filters.</summary>
public sealed record GetNotificationTemplatesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Channel = null,
    string? Module = null,
    bool? IsActive = null) : IQuery<PagedResult<NotificationTemplateDto>>;

/// <summary>Returns a paginated list of notification templates filtered by tenant context.</summary>
public sealed class GetNotificationTemplatesHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetNotificationTemplatesQuery, PagedResult<NotificationTemplateDto>>
{
    public async Task<Result<PagedResult<NotificationTemplateDto>>> Handle(
        GetNotificationTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.NotificationTemplates
            .Where(t => t.TenantId == tenantId);

        if (request.Channel is not null &&
            Enum.TryParse<NotificationChannel>(request.Channel, ignoreCase: true, out var channel))
        {
            query = query.Where(t => t.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(request.Module))
        {
            var module = request.Module.Trim().ToLowerInvariant();
            query = query.Where(t => t.Module == module);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        var ordered = query.OrderBy(t => t.Code);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new NotificationTemplateDto(
                t.Id.Value, t.Code, t.Module,
                t.Channel.ToString(), t.Subject,
                t.Format.ToString(), t.IsSystem, t.IsActive,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<NotificationTemplateDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<NotificationTemplateDto>>.Success(result,
            LocalizedMessage.Of("lockey_notifications_templates_listed"));
    }
}
