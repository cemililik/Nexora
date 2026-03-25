using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to deactivate a module for a tenant without removing data.</summary>
public sealed record DeactivateModuleCommand(Guid TenantId, string ModuleName) : ICommand;

/// <summary>Validates the <see cref="DeactivateModuleCommand"/> inputs.</summary>
public sealed class DeactivateModuleValidator : AbstractValidator<DeactivateModuleCommand>
{
    public DeactivateModuleValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.ModuleName).NotEmpty().WithMessage("lockey_validation_required");
    }
}

/// <summary>
/// Deactivates a module for a tenant without removing data.
/// Tables remain intact, module is simply hidden from the tenant.
/// </summary>
public sealed class DeactivateModuleHandler(
    PlatformDbContext platformDb,
    ILogger<DeactivateModuleHandler> logger) : ICommandHandler<DeactivateModuleCommand>
{
    public async Task<Result> Handle(DeactivateModuleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.From(request.TenantId);

        var tenantModule = await platformDb.TenantModules
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleName == request.ModuleName, ct);

        if (tenantModule is null)
        {
            logger.LogWarning("Module {ModuleName} not found for tenant {TenantId}", request.ModuleName, request.TenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_not_installed"));
        }

        if (!tenantModule.IsActive)
        {
            logger.LogWarning("Business rule: {Rule} for {Entity} {Id}", "Module already inactive", "TenantModule", request.ModuleName);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_module_already_inactive"));
        }

        tenantModule.Deactivate();
        await platformDb.SaveChangesAsync(ct);

        logger.LogInformation("Module {ModuleName} deactivated for tenant {TenantId}",
            request.ModuleName, request.TenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_module_deactivated"));
    }
}
