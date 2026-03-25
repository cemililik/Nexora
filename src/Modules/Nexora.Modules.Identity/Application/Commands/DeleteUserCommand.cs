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

        // Remove from Keycloak
        if (!string.IsNullOrEmpty(user.KeycloakUserId))
        {
            try
            {
                var tenant = await platformDbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

                if (tenant?.RealmId is not null)
                    await keycloakAdmin.DisableUserAsync(tenant.RealmId, user.KeycloakUserId, ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Failed to delete user {UserId} from Keycloak", request.Id);
                // Continue with local deletion even if KC fails
            }
        }

        // Remove org memberships (cascades to user roles via EF config)
        dbContext.OrganizationUsers.RemoveRange(user.OrganizationUsers);
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} deleted for tenant {TenantId}", user.Id, tenantId);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_user_deleted"));
    }
}
