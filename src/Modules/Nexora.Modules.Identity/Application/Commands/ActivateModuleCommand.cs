using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to activate a previously deactivated module for a tenant.</summary>
public sealed record ActivateModuleCommand(Guid TenantId, string ModuleName) : ICommand;

/// <summary>Validates the <see cref="ActivateModuleCommand"/> inputs.</summary>
public sealed class ActivateModuleValidator : AbstractValidator<ActivateModuleCommand>
{
    public ActivateModuleValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.ModuleName).NotEmpty().WithMessage("lockey_validation_required");
    }
}

/// <summary>
/// Activates a previously deactivated module for a tenant.
/// Runs migration check to ensure tables are up-to-date.
/// </summary>
public sealed class ActivateModuleHandler(
    PlatformDbContext platformDb,
    ITenantSchemaManager schemaManager,
    ILogger<ActivateModuleHandler> logger) : ICommandHandler<ActivateModuleCommand>
{
    public async Task<Result> Handle(ActivateModuleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenantModule = await platformDb.TenantModules
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleName == request.ModuleName, ct);

        if (tenantModule is null)
        {
            logger.LogWarning("Module {ModuleName} not found for tenant {TenantId}", request.ModuleName, request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_not_installed"));
        }

        if (tenantModule.IsActive)
        {
            logger.LogWarning("Business rule: {Rule} for {Entity} {Id}", "Module already active", "TenantModule", request.ModuleName);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_already_active"));
        }

        // Run migration check to ensure tables are up-to-date
        var schemaName = $"tenant_{request.TenantId:N}";
        try
        {
            await schemaManager.MigrateModuleAsync(schemaName, request.ModuleName, ct);
            logger.LogInformation("Migration check completed for module {ModuleName} in schema {Schema}",
                request.ModuleName, schemaName);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            logger.LogError(ex, "Migration check failed for module {ModuleName} in tenant {TenantId}",
                request.ModuleName, request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_migration_failed"));
        }

        tenantModule.Activate();
        await platformDb.SaveChangesAsync(ct);

        logger.LogInformation("Module {ModuleName} activated for tenant {TenantId}",
            request.ModuleName, request.TenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_module_activated"));
    }
}
