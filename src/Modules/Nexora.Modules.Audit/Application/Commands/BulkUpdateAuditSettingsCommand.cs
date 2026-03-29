using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Application.Services;
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

/// <summary>Validates bulk audit settings update input.</summary>
public sealed class BulkUpdateAuditSettingsValidator : AbstractValidator<BulkUpdateAuditSettingsCommand>
{
    public BulkUpdateAuditSettingsValidator()
    {
        RuleFor(x => x.Settings)
            .NotEmpty().WithMessage("lockey_audit_validation_settings_required");

        RuleForEach(x => x.Settings).ChildRules(item =>
        {
            item.RuleFor(s => s.Module)
                .NotEmpty().WithMessage("lockey_audit_validation_module_required");

            item.RuleFor(s => s.Operation)
                .NotEmpty().WithMessage("lockey_audit_validation_operation_required");

            item.RuleFor(s => s.RetentionDays)
                .GreaterThan(0).WithMessage("lockey_audit_validation_retention_days_must_be_positive")
                .LessThanOrEqualTo(3650).WithMessage("lockey_audit_validation_retention_days_max_exceeded");
        });

        RuleFor(x => x.Settings)
            .Must(settings => settings
                .Select(s => $"{s.Module}:{s.Operation}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == settings.Count)
            .WithMessage("lockey_audit_validation_duplicate_settings");
    }
}

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

        // Batch load all existing settings for this tenant to avoid N+1 queries
        var existingSettings = await dbContext.AuditSettings
            .Where(s => s.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingLookup = existingSettings
            .ToDictionary(s => $"{s.Module}:{s.Operation}", StringComparer.OrdinalIgnoreCase);

        foreach (var item in request.Settings)
        {
            var (module, operation) = AuditSetting.NormalizeKey(item.Module, item.Operation);
            var key = $"{module}:{operation}";

            if (existingLookup.TryGetValue(key, out var existing))
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
            var (mod, op) = AuditSetting.NormalizeKey(item.Module, item.Operation);
            var (enabledKey, disabledKey) = AuditCacheKeys.InvalidationKeys(tenantId, mod, op);
            await cacheService.RemoveAsync(enabledKey, cancellationToken);
            await cacheService.RemoveAsync(disabledKey, cancellationToken);
        }

        logger.LogInformation(
            "Bulk updated {Count} audit settings for tenant {TenantId}",
            request.Settings.Count, tenantId);

        return Result<List<AuditSettingDto>>.Success(results,
            LocalizedMessage.Of("lockey_audit_settings_saved"));
    }
}
