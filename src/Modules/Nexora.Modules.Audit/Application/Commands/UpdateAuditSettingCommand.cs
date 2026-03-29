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

/// <summary>Command to create or update an audit setting for a module/operation pair.</summary>
public sealed record UpdateAuditSettingCommand(
    string Module,
    string Operation,
    bool IsEnabled,
    int RetentionDays) : ICommand<AuditSettingDto>;

/// <summary>Validates audit setting update input.</summary>
public sealed class UpdateAuditSettingValidator : AbstractValidator<UpdateAuditSettingCommand>
{
    public UpdateAuditSettingValidator()
    {
        RuleFor(x => x.Module)
            .NotEmpty().WithMessage("lockey_audit_validation_module_required");

        RuleFor(x => x.Operation)
            .NotEmpty().WithMessage("lockey_audit_validation_operation_required");

        RuleFor(x => x.RetentionDays)
            .GreaterThan(0).WithMessage("lockey_audit_validation_retention_days_must_be_positive")
            .LessThanOrEqualTo(3650).WithMessage("lockey_audit_validation_retention_days_max_exceeded");
    }
}

/// <summary>Upserts an audit setting: creates if not exists, updates if exists. Invalidates cache.</summary>
public sealed class UpdateAuditSettingHandler(
    AuditDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ICacheService cacheService,
    ILogger<UpdateAuditSettingHandler> logger) : ICommandHandler<UpdateAuditSettingCommand, AuditSettingDto>
{
    public async Task<Result<AuditSettingDto>> Handle(
        UpdateAuditSettingCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.Current.TenantId;
        var userId = tenantContextAccessor.Current.UserId ?? "system";
        var module = request.Module.Trim().ToLowerInvariant();
        var operation = request.Operation.Trim().ToLowerInvariant();

        var existing = await dbContext.AuditSettings
            .FirstOrDefaultAsync(s =>
                s.TenantId == tenantId &&
                s.Module == module &&
                s.Operation == operation,
                cancellationToken);

        if (existing is not null)
        {
            existing.Update(request.IsEnabled, request.RetentionDays, userId);
            logger.LogInformation(
                "Audit setting updated for {Module}/{Operation} in tenant {TenantId}",
                module, operation, tenantId);
        }
        else
        {
            existing = AuditSetting.Create(
                tenantId, module, operation,
                request.IsEnabled, request.RetentionDays);
            dbContext.AuditSettings.Add(existing);
            logger.LogInformation(
                "Audit setting created for {Module}/{Operation} in tenant {TenantId}",
                module, operation, tenantId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache for this setting (both defaultEnabled variants)
        var (enabledKey, disabledKey) = AuditCacheKeys.InvalidationKeys(tenantId, module, operation);
        await cacheService.RemoveAsync(enabledKey, cancellationToken);
        await cacheService.RemoveAsync(disabledKey, cancellationToken);

        var dto = new AuditSettingDto(
            existing.Id.Value, existing.Module, existing.Operation,
            existing.IsEnabled, existing.RetentionDays);

        return Result<AuditSettingDto>.Success(dto,
            LocalizedMessage.Of("lockey_audit_setting_updated"));
    }
}
