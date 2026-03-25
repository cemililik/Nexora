using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.Modules.Identity.Infrastructure.Keycloak;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to update a user's profile (name, phone) with Keycloak sync.</summary>
public sealed record UpdateUserProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Phone) : ICommand<UserDto>;

/// <summary>Validates user profile update input.</summary>
public sealed class UpdateUserProfileValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("lockey_identity_validation_user_id_required");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("lockey_identity_validation_first_name_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_first_name_max_length");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("lockey_identity_validation_last_name_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_last_name_max_length");
    }
}

/// <summary>Updates a user's profile in the database and syncs to Keycloak.</summary>
public sealed class UpdateUserProfileHandler(
    IdentityDbContext dbContext,
    PlatformDbContext platformDb,
    ITenantContextAccessor tenantContextAccessor,
    IKeycloakAdminService keycloakAdmin,
    ILogger<UpdateUserProfileHandler> logger) : ICommandHandler<UpdateUserProfileCommand, UserDto>
{
    public async Task<Result<UserDto>> Handle(
        UpdateUserProfileCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("User profile update failed: user {UserId} not found for tenant {TenantId}", request.UserId, tenantId);
            return Result<UserDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        user.UpdateProfile(request.FirstName, request.LastName, request.Phone);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Sync to Keycloak
        var tenant = await platformDb.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant?.RealmId is not null)
        {
            await keycloakAdmin.UpdateUserAsync(
                tenant.RealmId, user.KeycloakUserId,
                user.Email, request.FirstName, request.LastName, cancellationToken);
        }

        var dto = new UserDto(
            user.Id.Value, user.Email, user.FirstName, user.LastName,
            user.Phone, user.Status.ToString(), user.LastLoginAt);

        logger.LogInformation("User {UserId} profile updated for tenant {TenantId}", user.Id, tenantId);

        return Result<UserDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_user_profile_updated"));
    }
}
