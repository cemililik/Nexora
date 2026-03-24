using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to uninstall (deactivate) a module for a specific tenant.</summary>
public sealed record UninstallModuleCommand(
    Guid TenantId,
    string ModuleName) : ICommand;

/// <summary>Validates module uninstall input.</summary>
public sealed class UninstallModuleValidator : AbstractValidator<UninstallModuleCommand>
{
    public UninstallModuleValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_id_required");

        RuleFor(x => x.ModuleName)
            .NotEmpty().WithMessage("lockey_identity_validation_module_name_required");
    }
}

/// <summary>Soft-uninstalls a module by deactivating its tenant module record and cleaning up role-permission associations.</summary>
public sealed class UninstallModuleHandler(
    PlatformDbContext platformDb,
    IdentityDbContext identityDb,
    IEnumerable<IModule> registeredModules,
    ILogger<UninstallModuleHandler> logger) : ICommandHandler<UninstallModuleCommand>
{
    public async Task<Result> Handle(
        UninstallModuleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenantModule = await platformDb.TenantModules
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleName == request.ModuleName,
                cancellationToken);

        if (tenantModule is null)
        {
            logger.LogWarning("Module {ModuleName} is not installed for tenant {TenantId}", request.ModuleName, request.TenantId);
            return Result.Failure("lockey_identity_error_module_not_installed");
        }

        // Call module's OnUninstallAsync if available
        var module = registeredModules.FirstOrDefault(m => m.Name == request.ModuleName);
        if (module is not null)
        {
            var schemaName = $"tenant_{tenantId.Value}";
            await module.OnUninstallAsync(
                new TenantInstallContext(tenantId.Value.ToString(), schemaName, null),
                cancellationToken);
        }

        // Remove role-permission associations for the uninstalled module's permissions
        var modulePermissionIds = await identityDb.Permissions
            .Where(p => p.Module == request.ModuleName)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (modulePermissionIds.Count > 0)
        {
            var orphanedRolePermissions = await identityDb.RolePermissions
                .Where(rp => modulePermissionIds.Contains(rp.PermissionId))
                .ToListAsync(cancellationToken);

            if (orphanedRolePermissions.Count > 0)
            {
                identityDb.RolePermissions.RemoveRange(orphanedRolePermissions);
                logger.LogInformation("Removed {Count} role-permission associations for module {ModuleName}",
                    orphanedRolePermissions.Count, request.ModuleName);
            }

            await identityDb.SaveChangesAsync(cancellationToken);
        }

        tenantModule.Deactivate();
        await platformDb.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Module {ModuleName} uninstalled for tenant {TenantId}", request.ModuleName, request.TenantId);

        return Result.Success(new LocalizedMessage("lockey_identity_module_uninstalled"));
    }
}
