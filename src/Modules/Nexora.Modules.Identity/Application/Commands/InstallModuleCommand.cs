using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.Modules;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to install a module for a specific tenant.</summary>
public sealed record InstallModuleCommand(
    Guid TenantId,
    string ModuleName) : ICommand<TenantModuleDto>;

/// <summary>Validates module installation input.</summary>
public sealed class InstallModuleValidator : AbstractValidator<InstallModuleCommand>
{
    public InstallModuleValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_id_required");

        RuleFor(x => x.ModuleName)
            .NotEmpty().WithMessage("lockey_identity_validation_module_name_required")
            .MaximumLength(50).WithMessage("lockey_identity_validation_module_name_max_length");
    }
}

/// <summary>Installs a module for a tenant after verifying it's not already installed.</summary>
public sealed class InstallModuleHandler(
    PlatformDbContext platformDb,
    IEnumerable<IModule> registeredModules,
    ILogger<InstallModuleHandler> logger) : ICommandHandler<InstallModuleCommand, TenantModuleDto>
{
    public async Task<Result<TenantModuleDto>> Handle(
        InstallModuleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);

        // Verify tenant exists
        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            logger.LogWarning("Tenant {TenantId} not found", request.TenantId);
            return Result<TenantModuleDto>.Failure("lockey_identity_error_tenant_not_found");
        }

        // Verify module is registered in the platform
        var module = registeredModules.FirstOrDefault(m => m.Name == request.ModuleName);
        if (module is null)
        {
            logger.LogWarning("Module {ModuleName} is not registered in the platform", request.ModuleName);
            return Result<TenantModuleDto>.Failure(
                "lockey_identity_error_module_not_found",
                new Dictionary<string, string> { ["module"] = request.ModuleName });
        }

        // Check if already installed
        var alreadyInstalled = await platformDb.TenantModules
            .AnyAsync(tm => tm.TenantId == tenantId && tm.ModuleName == request.ModuleName, cancellationToken);

        if (alreadyInstalled)
        {
            logger.LogWarning("Module {ModuleName} is already installed for tenant {TenantId}", request.ModuleName, request.TenantId);
            return Result<TenantModuleDto>.Failure(
                "lockey_identity_error_module_already_installed",
                new Dictionary<string, string> { ["module"] = request.ModuleName });
        }

        // Check dependencies
        foreach (var dep in module.Dependencies)
        {
            var depInstalled = await platformDb.TenantModules
                .AnyAsync(tm => tm.TenantId == tenantId && tm.ModuleName == dep && tm.IsActive, cancellationToken);

            if (!depInstalled)
            {
                logger.LogWarning("Dependency {DependencyName} for module {ModuleName} is not installed for tenant {TenantId}", dep, request.ModuleName, request.TenantId);
                return Result<TenantModuleDto>.Failure(
                    "lockey_identity_error_module_dependency_missing",
                    new Dictionary<string, string> { ["module"] = request.ModuleName, ["dependency"] = dep });
            }
        }

        var tenantModule = TenantModule.Create(tenantId, request.ModuleName);
        await platformDb.TenantModules.AddAsync(tenantModule, cancellationToken);
        await platformDb.SaveChangesAsync(cancellationToken);

        var dto = new TenantModuleDto(
            tenantModule.Id.Value, tenantModule.ModuleName,
            tenantModule.IsActive, tenantModule.InstalledAt, tenantModule.InstalledBy);

        logger.LogInformation("Module {ModuleName} installed for tenant {TenantId}", request.ModuleName, request.TenantId);

        return Result<TenantModuleDto>.Success(dto,
            new LocalizedMessage("lockey_identity_module_installed"));
    }
}
