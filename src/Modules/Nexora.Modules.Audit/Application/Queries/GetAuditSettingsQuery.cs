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

/// <summary>Query to retrieve all audit settings for the current tenant.</summary>
public sealed record GetAuditSettingsQuery : IQuery<IReadOnlyList<AuditSettingDto>>;

/// <summary>Validator for <see cref="GetAuditSettingsQuery"/>. No input parameters to validate.</summary>
public sealed class GetAuditSettingsValidator : AbstractValidator<GetAuditSettingsQuery> { }

/// <summary>Returns all audit settings for the current tenant.</summary>
public sealed class GetAuditSettingsHandler(
    IAuditSettingRepository auditSettingRepository,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetAuditSettingsHandler> logger) : IQueryHandler<GetAuditSettingsQuery, IReadOnlyList<AuditSettingDto>>
{
    public async Task<Result<IReadOnlyList<AuditSettingDto>>> Handle(
        GetAuditSettingsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;

        var sw = Stopwatch.StartNew();

        var settings = await auditSettingRepository.GetAllByTenantAsync(tenantId, cancellationToken);

        sw.Stop();
        if (sw.ElapsedMilliseconds > 500)
        {
            logger.LogWarning("Slow query detected: {QueryName} took {ElapsedMs}ms for tenant {TenantId}",
                nameof(GetAuditSettingsQuery), sw.ElapsedMilliseconds, tenantId);
        }

        var dtos = settings.Select(s => new AuditSettingDto(
            s.Id.Value, s.Module, s.Operation, s.IsEnabled, s.RetentionDays)).ToList();

        return Result<IReadOnlyList<AuditSettingDto>>.Success(dtos,
            LocalizedMessage.Of("lockey_audit_settings_listed"));
    }
}
