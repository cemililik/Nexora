using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.Modules.Audit.Domain.ValueObjects;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Queries;

/// <summary>Query to retrieve a single audit log entry by its identifier.</summary>
public sealed record GetAuditLogDetailQuery(Guid Id) : IQuery<AuditLogDetailDto>;

public sealed class GetAuditLogDetailValidator : AbstractValidator<GetAuditLogDetailQuery>
{
    public GetAuditLogDetailValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_id_required");
    }
}

/// <summary>Returns full audit log detail for a single entry.</summary>
public sealed class GetAuditLogDetailHandler(
    IAuditEntryRepository auditEntryRepository,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetAuditLogDetailHandler> logger) : IQueryHandler<GetAuditLogDetailQuery, AuditLogDetailDto>
{
    public async Task<Result<AuditLogDetailDto>> Handle(
        GetAuditLogDetailQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var entryId = AuditEntryId.From(request.Id);

        var entry = await auditEntryRepository.GetByIdAsync(entryId, tenantId, cancellationToken);

        if (entry is null)
        {
            logger.LogDebug("Audit entry not found for {Id} in tenant {TenantId}", request.Id, tenantId);
            return Result<AuditLogDetailDto>.Failure(
                LocalizedMessage.Of("lockey_audit_error_entry_not_found"));
        }

        var dto = new AuditLogDetailDto(
            entry.Id.Value, entry.Module, entry.Operation, entry.OperationType,
            entry.UserEmail, entry.IsSuccess, entry.EntityType, entry.EntityId,
            entry.Timestamp, entry.UserId, entry.IpAddress, entry.UserAgent,
            entry.CorrelationId, entry.ErrorKey, entry.BeforeState, entry.AfterState,
            entry.Changes, entry.Metadata);

        return Result<AuditLogDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_audit_log_detail_retrieved"));
    }
}
