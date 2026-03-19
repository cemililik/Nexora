using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to activate or deactivate a user with Keycloak sync.</summary>
public sealed record UpdateUserStatusCommand(
    Guid UserId,
    string Action) : ICommand;

/// <summary>Validates user status update input (activate/deactivate).</summary>
public sealed class UpdateUserStatusValidator : AbstractValidator<UpdateUserStatusCommand>
{
    private static readonly string[] ValidActions = ["activate", "deactivate"];

    public UpdateUserStatusValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("lockey_identity_validation_user_action_required")
            .Must(a => ValidActions.Contains(a.ToLowerInvariant()))
            .WithMessage("lockey_identity_validation_invalid_user_action");
    }
}

/// <summary>Activates or deactivates a user and syncs status to Keycloak.</summary>
public sealed class UpdateUserStatusHandler(
    IdentityDbContext dbContext,
    PlatformDbContext platformDb,
    ITenantContextAccessor tenantContextAccessor,
    IKeycloakAdminService keycloakAdmin,
    ILogger<UpdateUserStatusHandler> logger) : ICommandHandler<UpdateUserStatusCommand>
{
    public async Task<Result> Handle(
        UpdateUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User status update failed: user {UserId} not found for tenant {TenantId}", request.UserId, tenantId);
            return Result.Failure("lockey_identity_error_user_not_found");
        }

        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        switch (request.Action.ToLowerInvariant())
        {
            case "activate":
                user.Activate();
                if (tenant?.RealmId is not null)
                    await keycloakAdmin.EnableUserAsync(tenant.RealmId, user.KeycloakUserId, cancellationToken);
                break;
            case "deactivate":
                user.Deactivate();
                if (tenant?.RealmId is not null)
                    await keycloakAdmin.DisableUserAsync(tenant.RealmId, user.KeycloakUserId, cancellationToken);
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} status changed to {Action}", user.Id, request.Action);

        return Result.Success(new LocalizedMessage("lockey_identity_user_status_updated"));
    }
}
