using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Notifications.Application.DTOs;
using Nexora.Modules.Notifications.Domain.ValueObjects;
using Nexora.Modules.Notifications.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Notifications.Application.Queries;

/// <summary>Query to list notification providers for the current tenant.</summary>
public sealed record GetNotificationProvidersQuery(string? Channel = null) : IQuery<IReadOnlyList<NotificationProviderDto>>;

/// <summary>Returns notification providers filtered by tenant and optional channel.</summary>
public sealed class GetNotificationProvidersHandler(
    NotificationsDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetNotificationProvidersQuery, IReadOnlyList<NotificationProviderDto>>
{
    public async Task<Result<IReadOnlyList<NotificationProviderDto>>> Handle(
        GetNotificationProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.NotificationProviders
            .Where(p => p.TenantId == tenantId);

        if (request.Channel is not null &&
            Enum.TryParse<NotificationChannel>(request.Channel, ignoreCase: true, out var channel))
        {
            query = query.Where(p => p.Channel == channel);
        }

        var items = await query
            .OrderBy(p => p.Channel).ThenBy(p => p.ProviderName)
            .Select(p => new NotificationProviderDto(
                p.Id.Value, p.Channel.ToString(), p.ProviderName.ToString(),
                p.IsDefault, p.IsActive, p.DailyLimit, p.SentToday,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<NotificationProviderDto>>.Success(items,
            LocalizedMessage.Of("lockey_notifications_providers_listed"));
    }
}
