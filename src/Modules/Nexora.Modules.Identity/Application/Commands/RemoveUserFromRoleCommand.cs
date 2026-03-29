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

/// <summary>Command to remove a user from a specific role across all their organization memberships.</summary>
public sealed record RemoveUserFromRoleCommand(
    Guid RoleId,
    Guid UserId) : ICommand;

/// <summary>Validates the <see cref="RemoveUserFromRoleCommand"/> inputs.</summary>
public sealed class RemoveUserFromRoleValidator : AbstractValidator<RemoveUserFromRoleCommand>
{
    public RemoveUserFromRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("lockey_validation_required");
    }
}

/// <summary>Handles removing a user from a role by deleting all UserRole entries matching the role and user.</summary>
public sealed class RemoveUserFromRoleHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RemoveUserFromRoleHandler> logger) : ICommandHandler<RemoveUserFromRoleCommand>
{
    public async Task<Result> Handle(RemoveUserFromRoleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.RoleId);
        var userId = UserId.From(request.UserId);

        // Find all organization-user entries for this user
        var orgUserIds = await dbContext.OrganizationUsers
            .AsNoTracking()
            .Where(ou => ou.UserId == userId)
            .Select(ou => ou.Id)
            .ToListAsync(ct);

        if (orgUserIds.Count == 0)
        {
            logger.LogWarning("User {UserId} has no organization memberships", request.UserId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        // Find and remove all UserRole entries for this role and user
        var userRoles = await dbContext.UserRoles
            .Where(ur => ur.RoleId == roleId && orgUserIds.Contains(ur.OrganizationUserId))
            .ToListAsync(ct);

        if (userRoles.Count == 0)
        {
            logger.LogWarning("User {UserId} does not have role {RoleId}", request.UserId, request.RoleId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_not_in_role"));
        }

        dbContext.UserRoles.RemoveRange(userRoles);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} removed from role {RoleId}, {Count} assignment(s) deleted",
            request.UserId, request.RoleId, userRoles.Count);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_user_removed_from_role"));
    }
}
