using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Queries;

/// <summary>Query to list pending scheduled notifications.</summary>
public sealed record GetScheduledNotificationsQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<NotificationScheduleDto>>;

/// <summary>Returns pending scheduled notifications for the current tenant.</summary>
public sealed class GetScheduledNotificationsHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetScheduledNotificationsQuery, PagedResult<NotificationScheduleDto>>
{
    public async Task<Result<PagedResult<NotificationScheduleDto>>> Handle(
        GetScheduledNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = from s in dbContext.NotificationSchedules
                    join n in dbContext.Notifications on s.NotificationId equals n.Id
                    where n.TenantId == tenantId && s.Status == ScheduleStatus.Pending
                    orderby s.ScheduledAt
                    select new NotificationScheduleDto(
                        s.Id.Value, n.Id.Value, s.ScheduledAt,
                        s.Status.ToString(), s.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<NotificationScheduleDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<NotificationScheduleDto>>.Success(result,
            LocalizedMessage.Of("lockey_notifications_schedules_listed"));
    }
}
