using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
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

/// <summary>Applies the requested status transition to a tenant.</summary>
public sealed class UpdateTenantStatusHandler(
    PlatformDbContext platformDb) : ICommandHandler<UpdateTenantStatusCommand>
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
            return Result.Failure("lockey_identity_error_tenant_not_found");
        }

        switch (request.Action.ToLowerInvariant())
        {
            case "activate":
                tenant.Activate();
                break;
            case "suspend":
                tenant.Suspend();
                break;
            case "terminate":
                tenant.Terminate();
                break;
        }

        await platformDb.SaveChangesAsync(cancellationToken);

        return Result.Success(new LocalizedMessage("lockey_identity_tenant_status_updated"));
    }
}
