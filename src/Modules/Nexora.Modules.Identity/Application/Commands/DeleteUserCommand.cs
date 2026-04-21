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

/// <summary>Command to delete a user and disable their Keycloak account.</summary>
public sealed record DeleteUserCommand(Guid Id) : ICommand;

/// <summary>Validates the <see cref="DeleteUserCommand"/> inputs.</summary>
public sealed class DeleteUserValidator : AbstractValidator<DeleteUserCommand>
{
    public DeleteUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
    }
}

/// <summary>Handles user deletion, including Keycloak disablement and organization membership cleanup.</summary>
public sealed class DeleteUserHandler(
    IdentityDbContext dbContext,
    PlatformDbContext platformDbContext,
    ITenantContextAccessor tenantContextAccessor,
    IKeycloakAdminService keycloakAdmin,
    ILogger<DeleteUserHandler> logger) : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.Id);

        var user = await dbContext.Users
            .Include(u => u.OrganizationUsers)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct);

        if (user is null)
        {
            logger.LogWarning("User {UserId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        // Prevent self-deletion
        var currentKeycloakId = tenantContextAccessor.Current.UserId;
        if (user.KeycloakUserId == currentKeycloakId)
        {
            logger.LogWarning("Business rule: {Rule} for {Entity} {Id}", "Self-delete attempt", "User", request.Id);
            return Result.Failure(LocalizedMessage.Of("lockey_identity_error_cannot_delete_self"));
        }

        // CONSISTENCY: Tier 2A — DB-First.
        // Soft-delete locally first (our source of truth). Then disable the Keycloak account
        // so the user cannot log in. We disable (not hard-delete) Keycloak to preserve the
        // account for audit purposes and to prevent email reuse on the Keycloak side.
        // If Keycloak disable fails the user is still blocked at our API (DB state checked on
        // every request). Keycloak divergence is non-fatal — logged as Warning.
        dbContext.OrganizationUsers.RemoveRange(user.OrganizationUsers);
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(user.KeycloakUserId))
        {
            var tenant = await platformDbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant?.RealmId is not null)
            {
                try
                {
                    await keycloakAdmin.DisableUserAsync(tenant.RealmId, user.KeycloakUserId, ct);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex,
                        "Keycloak disable failed for user {UserId} after local deletion; state will diverge until reconciled",
                        request.Id);
                }
            }
            else
            {
                logger.LogWarning(
                    "Keycloak disable skipped for user {UserId}: tenant {TenantId} has no realm configured",
                    request.Id, tenantId);
            }
        }

        logger.LogInformation("User {UserId} deleted for tenant {TenantId}", user.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_user_deleted"));
    }
}
