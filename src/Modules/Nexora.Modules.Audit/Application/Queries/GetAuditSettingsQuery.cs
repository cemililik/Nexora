using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Queries;

/// <summary>Query to retrieve all audit settings for the current tenant.</summary>
public sealed record GetAuditSettingsQuery : IQuery<IReadOnlyList<AuditSettingDto>>;

/// <summary>Returns all audit settings for the current tenant.</summary>
public sealed class GetAuditSettingsHandler(
    AuditDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetAuditSettingsQuery, IReadOnlyList<AuditSettingDto>>
{
    public async Task<Result<IReadOnlyList<AuditSettingDto>>> Handle(
        GetAuditSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;

        var settings = await dbContext.AuditSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .OrderBy(s => s.Module)
            .ThenBy(s => s.Operation)
            .Select(s => new AuditSettingDto(
                s.Id.Value, s.Module, s.Operation, s.IsEnabled, s.RetentionDays))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AuditSettingDto>>.Success(settings,
            LocalizedMessage.Of("lockey_audit_settings_listed"));
    }
}
