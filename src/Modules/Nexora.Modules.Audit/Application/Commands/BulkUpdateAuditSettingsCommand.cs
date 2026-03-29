using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Infrastructure;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Commands;

/// <summary>Single setting item within a bulk update request.</summary>
public sealed record AuditSettingItem(string Module, string Operation, bool IsEnabled, int RetentionDays);

/// <summary>Command to bulk-update multiple audit settings in a single transaction.</summary>
public sealed record BulkUpdateAuditSettingsCommand(
    List<AuditSettingItem> Settings) : ICommand<List<AuditSettingDto>>;

/// <summary>Handles bulk upsert of audit settings.</summary>
public sealed class BulkUpdateAuditSettingsHandler(
    AuditDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ICacheService cacheService,
    ILogger<BulkUpdateAuditSettingsHandler> logger) : ICommandHandler<BulkUpdateAuditSettingsCommand, List<AuditSettingDto>>
{
    public async Task<Result<List<AuditSettingDto>>> Handle(
        BulkUpdateAuditSettingsCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var userId = tenantContextAccessor.Current.UserId ?? "system";
        var results = new List<AuditSettingDto>();

        foreach (var item in request.Settings)
        {
            var existing = await dbContext.AuditSettings
                .FirstOrDefaultAsync(s =>
                    s.TenantId == tenantId &&
                    s.Module == item.Module &&
                    s.Operation == item.Operation,
                    cancellationToken);

            if (existing is not null)
            {
                existing.Update(item.IsEnabled, item.RetentionDays, userId);
            }
            else
            {
                existing = AuditSetting.Create(tenantId, item.Module, item.Operation, item.IsEnabled, item.RetentionDays);
                await dbContext.AuditSettings.AddAsync(existing, cancellationToken);
            }

            results.Add(new AuditSettingDto(
                existing.Id.Value, existing.Module, existing.Operation,
                existing.IsEnabled, existing.RetentionDays));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache for each updated setting (both defaultEnabled variants)
        foreach (var item in request.Settings)
        {
            await cacheService.RemoveAsync(
                $"audit:config:{tenantId}:{item.Module}:{item.Operation}:1", cancellationToken);
            await cacheService.RemoveAsync(
                $"audit:config:{tenantId}:{item.Module}:{item.Operation}:0", cancellationToken);
        }

        logger.LogInformation(
            "Bulk updated {Count} audit settings for tenant {TenantId}",
            request.Settings.Count, tenantId);

        return Result<List<AuditSettingDto>>.Success(results,
            LocalizedMessage.Of("lockey_audit_settings_saved"));
    }
}
