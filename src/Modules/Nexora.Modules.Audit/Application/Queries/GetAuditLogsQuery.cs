using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Queries;

/// <summary>Paginated query to list audit log entries with optional filters.</summary>
public sealed record GetAuditLogsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Module = null,
    string? Operation = null,
    Guid? UserId = null,
    string? EntityType = null,
    bool? IsSuccess = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null) : IQuery<PagedResult<AuditLogDto>>;

/// <summary>Returns a paginated list of audit log entries filtered by tenant context and optional criteria.</summary>
public sealed class GetAuditLogsHandler(
    AuditDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;

        var query = dbContext.AuditEntries.AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (request.Module is not null)
            query = query.Where(e => e.Module == request.Module);

        if (request.Operation is not null)
            query = query.Where(e => e.Operation == request.Operation);

        if (request.UserId is not null)
            query = query.Where(e => e.UserId == request.UserId);

        if (request.EntityType is not null)
            query = query.Where(e => e.EntityType == request.EntityType);

        if (request.IsSuccess is not null)
            query = query.Where(e => e.IsSuccess == request.IsSuccess);

        if (request.DateFrom is not null)
            query = query.Where(e => e.Timestamp >= request.DateFrom);

        if (request.DateTo is not null)
            query = query.Where(e => e.Timestamp <= request.DateTo);

        var ordered = query.OrderByDescending(e => e.Timestamp);
        var totalCount = await ordered.CountAsync(cancellationToken);

        var items = await ordered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new AuditLogDto(
                e.Id.Value, e.Module, e.Operation, e.OperationType,
                e.UserEmail, e.IsSuccess, e.EntityType, e.EntityId,
                e.Timestamp))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<AuditLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<AuditLogDto>>.Success(result,
            LocalizedMessage.Of("lockey_audit_logs_listed"));
    }
}
