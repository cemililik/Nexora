using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Queries;

/// <summary>Paginated query to list sent notifications.</summary>
public sealed record GetNotificationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Channel = null,
    string? Status = null) : IQuery<PagedResult<NotificationDto>>;

/// <summary>Returns a paginated list of notifications filtered by tenant context.</summary>
public sealed class GetNotificationsHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
{
    public async Task<Result<PagedResult<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Notifications.AsNoTracking()
            .Where(n => n.TenantId == tenantId);

        if (request.Channel is not null &&
            Enum.TryParse<NotificationChannel>(request.Channel, ignoreCase: true, out var channel))
        {
            query = query.Where(n => n.Channel == channel);
        }

        if (request.Status is not null &&
            Enum.TryParse<NotificationStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(n => n.Status == status);
        }

        var ordered = query.OrderByDescending(n => n.QueuedAt);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto(
                n.Id.Value, n.Channel.ToString(), n.Subject, n.Status.ToString(),
                n.TriggeredBy, n.TotalRecipients, n.DeliveredCount, n.FailedCount,
                n.QueuedAt, n.SentAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<NotificationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<NotificationDto>>.Success(result,
            LocalizedMessage.Of("lockey_notifications_notifications_listed"));
    }
}
