using FluentValidation;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Audit.Application.DTOs;
using Nexora.Modules.Audit.Application.Services;
using Nexora.Modules.Audit.Domain.Entities;
using Nexora.Modules.Audit.Domain.Repositories;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Audit.Application.Commands;

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
                .Select(s => $"{s.Module.Trim().ToLowerInvariant()}:{s.Operation.Trim().ToLowerInvariant()}")
                .Distinct(StringComparer.Ordinal)
                .Count() == settings.Count)
            .WithMessage("lockey_audit_validation_duplicate_settings");
    }
}

/// <summary>Handles bulk upsert of audit settings.</summary>
public sealed class BulkUpdateAuditSettingsHandler(
    IAuditSettingRepository auditSettingRepository,
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
        var newSettingCount = 0;

        // Batch load all existing settings for this tenant to avoid N+1 queries
        var existingSettings = await auditSettingRepository.GetAllByTenantAsync(tenantId, cancellationToken);

        var existingLookup = existingSettings
            .ToDictionary(s => $"{s.Module}:{s.Operation}", StringComparer.OrdinalIgnoreCase);

        var normalizedKeys = new List<(string Module, string Operation)>(request.Settings.Count);

        foreach (var item in request.Settings)
        {
            var (module, operation) = AuditSetting.NormalizeKey(item.Module, item.Operation);
            normalizedKeys.Add((module, operation));
            var key = $"{module}:{operation}";

            if (existingLookup.TryGetValue(key, out var existing))
            {
                existing.Update(item.IsEnabled, item.RetentionDays, userId);
            }
            else
            {
                existing = AuditSetting.Create(tenantId, item.Module, item.Operation, item.IsEnabled, item.RetentionDays);
                auditSettingRepository.Add(existing);
                newSettingCount++;
            }

            results.Add(new AuditSettingDto(
                existing.Id.Value, existing.Module, existing.Operation,
                existing.IsEnabled, existing.RetentionDays));
        }

        if (newSettingCount > 0)
        {
            logger.LogWarning("Creating {NewCount} new audit settings for tenant {TenantId} — ensure module/operation keys are correct",
                newSettingCount, tenantId);
        }

        await auditSettingRepository.SaveChangesAsync(cancellationToken);

        // Invalidate cache for each updated setting (both defaultEnabled variants)
        var invalidationTasks = normalizedKeys.SelectMany(k =>
        {
            var (enabledKey, disabledKey) = AuditCacheKeys.InvalidationKeys(k.Module, k.Operation);
            return new[]
            {
                cacheService.RemoveAsync(enabledKey, cancellationToken),
                cacheService.RemoveAsync(disabledKey, cancellationToken)
            };
        });
        await Task.WhenAll(invalidationTasks);

        logger.LogInformation(
            "Bulk updated {Count} audit settings for tenant {TenantId}",
            request.Settings.Count, tenantId);

        return Result<List<AuditSettingDto>>.Success(results,
            LocalizedMessage.Of("lockey_audit_settings_saved"));
    }
}
