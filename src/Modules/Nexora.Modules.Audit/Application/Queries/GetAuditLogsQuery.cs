using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Domain.Repositories;
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

public sealed class GetAuditLogsValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("lockey_validation_page_must_be_positive");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("lockey_validation_page_size_range");
    }
}

/// <summary>Returns a paginated list of audit log entries filtered by tenant context and optional criteria.</summary>
public sealed class GetAuditLogsHandler(
    IAuditEntryRepository auditEntryRepository,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetAuditLogsHandler> logger) : IQueryHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    public async Task<Result<PagedResult<AuditLogDto>>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var sw = Stopwatch.StartNew();

        var (entries, totalCount) = await auditEntryRepository.GetPagedAsync(
            tenantId, request.Page, request.PageSize,
            request.Module, request.Operation, request.UserId,
            request.EntityType, request.IsSuccess,
            request.DateFrom, request.DateTo,
            cancellationToken);

        var items = entries.Select(e => new AuditLogDto(
            e.Id.Value, e.Module, e.Operation, e.OperationType,
            e.UserEmail, e.IsSuccess, e.EntityType, e.EntityId,
            e.Timestamp)).ToList();

        sw.Stop();
        if (sw.ElapsedMilliseconds > 500)
            logger.LogWarning("Slow query detected in GetAuditLogsHandler: {ElapsedMs}ms for tenant {TenantId}", sw.ElapsedMilliseconds, tenantId);

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
