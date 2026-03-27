using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to list audit logs with optional filters and pagination.</summary>
public sealed record GetAuditLogsQuery(
    Guid? UserId = null,
    string? Action = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<AuditLogDto>>;

/// <summary>Returns paginated and filtered audit logs for the current tenant.</summary>
public sealed class GetAuditLogsHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.AuditLogs.AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (request.UserId.HasValue)
        {
            var userId = UserId.From(request.UserId.Value);
            query = query.Where(a => a.UserId == userId);
        }

        if (!string.IsNullOrEmpty(request.Action))
            query = query.Where(a => a.Action == request.Action);

        if (request.From.HasValue)
            query = query.Where(a => a.Timestamp >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(a => a.Timestamp <= request.To.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id.Value, a.UserId.Value, a.Action,
                a.IpAddress, a.UserAgent, a.Timestamp, a.Details))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AuditLogDto>
        {
            TotalCount = totalCount,
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<AuditLogDto>>.Success(result,
            new LocalizedMessage("lockey_identity_audit_logs_listed"));
    }
}
