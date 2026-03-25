using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to change a tenant's lifecycle status (activate, suspend, terminate).</summary>
public sealed record UpdateTenantStatusCommand(
    Guid TenantId,
    string Action) : ICommand;

/// <summary>Validates tenant status update input (valid action, non-empty tenant ID).</summary>
public sealed class UpdateTenantStatusValidator : AbstractValidator<UpdateTenantStatusCommand>
{
    private static readonly string[] ValidActions = ["activate", "suspend", "terminate"];

    public UpdateTenantStatusValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("lockey_identity_validation_tenant_id_required");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("lockey_identity_validation_action_required")
            .Must(a => ValidActions.Contains(a.ToLowerInvariant()))
            .WithMessage("lockey_identity_validation_invalid_tenant_action");
    }
}

/// <summary>
/// Applies the requested status transition to a tenant.
/// Terminate: deactivates all modules and soft-deletes the tenant (schema preserved).
/// </summary>
public sealed class UpdateTenantStatusHandler(
    PlatformDbContext platformDb,
    ILogger<UpdateTenantStatusHandler> logger) : ICommandHandler<UpdateTenantStatusCommand>
{
    public async Task<Result> Handle(
        UpdateTenantStatusCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.From(request.TenantId);
        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            logger.LogWarning("Tenant {TenantId} not found", request.TenantId);
            return Result.Failure("lockey_identity_error_tenant_not_found");
        }

        switch (request.Action.ToLowerInvariant())
        {
            case "activate":
                tenant.Activate();
                logger.LogInformation("Tenant {TenantId} activated", request.TenantId);
                break;

            case "suspend":
                tenant.Suspend();
                logger.LogInformation("Tenant {TenantId} suspended", request.TenantId);
                break;

            case "terminate":
                tenant.Terminate();

                // Deactivate all active modules for this tenant
                var activeModules = await platformDb.TenantModules
                    .Where(tm => tm.TenantId == tenantId && tm.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var module in activeModules)
                    module.Deactivate();

                logger.LogInformation("Tenant {TenantId} terminated, {ModuleCount} modules deactivated",
                    request.TenantId, activeModules.Count);
                break;
        }

        await platformDb.SaveChangesAsync(cancellationToken);

        return Result.Success(new LocalizedMessage("lockey_identity_tenant_status_updated"));
    }
}
