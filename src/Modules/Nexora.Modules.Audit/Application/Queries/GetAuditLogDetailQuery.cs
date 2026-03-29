using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Domain.ValueObjects;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Queries;

/// <summary>Query to retrieve a single audit log entry by its identifier.</summary>
public sealed record GetAuditLogDetailQuery(Guid Id) : IQuery<AuditLogDetailDto>;

/// <summary>Returns full audit log detail for a single entry.</summary>
public sealed class GetAuditLogDetailHandler(
    AuditDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetAuditLogDetailQuery, AuditLogDetailDto>
{
    public async Task<Result<AuditLogDetailDto>> Handle(
        GetAuditLogDetailQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var entryId = AuditEntryId.From(request.Id);

        var entry = await dbContext.AuditEntries.AsNoTracking()
            .Where(e => e.Id == entryId && e.TenantId == tenantId)
            .Select(e => new AuditLogDetailDto(
                e.Id.Value, e.Module, e.Operation, e.OperationType,
                e.UserEmail, e.IsSuccess, e.EntityType, e.EntityId,
                e.Timestamp, e.UserId, e.IpAddress, e.UserAgent,
                e.CorrelationId, e.ErrorKey, e.BeforeState, e.AfterState,
                e.Changes, e.Metadata))
            .FirstOrDefaultAsync(cancellationToken);

        if (entry is null)
            return Result<AuditLogDetailDto>.Failure(
                LocalizedMessage.Of("lockey_audit_error_entry_not_found"));

        return Result<AuditLogDetailDto>.Success(entry,
            LocalizedMessage.Of("lockey_audit_log_detail_retrieved"));
    }
}
