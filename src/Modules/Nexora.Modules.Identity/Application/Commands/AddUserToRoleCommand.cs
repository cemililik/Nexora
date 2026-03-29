using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to add a user to a specific role. Assigns the role in all organizations the user belongs to.</summary>
public sealed record AddUserToRoleCommand(
    Guid RoleId,
    Guid UserId) : ICommand;

/// <summary>Validates the <see cref="AddUserToRoleCommand"/> inputs.</summary>
public sealed class AddUserToRoleValidator : AbstractValidator<AddUserToRoleCommand>
{
    public AddUserToRoleValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("lockey_validation_required");
    }
}

/// <summary>Handles adding a user to a role by creating UserRole entries for each organization membership.</summary>
public sealed class AddUserToRoleHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AddUserToRoleHandler> logger) : ICommandHandler<AddUserToRoleCommand>
{
    public async Task<Result> Handle(AddUserToRoleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.RoleId);
        var userId = UserId.From(request.UserId);

        // Verify role exists
        var roleExists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

        if (!roleExists)
        {
            logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenantId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_not_found"));
        }

        // Get all org memberships for this user
        var orgUsers = await dbContext.OrganizationUsers
            .Where(ou => ou.UserId == userId)
            .ToListAsync(ct);

        if (orgUsers.Count == 0)
        {
            logger.LogWarning("User {UserId} has no organization memberships", request.UserId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_not_in_org"));
        }

        // Get existing role assignments to avoid duplicates
        var orgUserIds = orgUsers.Select(ou => ou.Id).ToList();
        var existingAssignments = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId && orgUserIds.Contains(ur.OrganizationUserId))
            .Select(ur => ur.OrganizationUserId)
            .ToHashSetAsync(ct);

        var added = 0;
        foreach (var orgUser in orgUsers)
        {
            if (!existingAssignments.Contains(orgUser.Id))
            {
                dbContext.UserRoles.Add(UserRole.Create(orgUser.Id, roleId));
                added++;
            }
        }

        if (added == 0)
        {
            logger.LogWarning("User {UserId} already has role {RoleId} in all organizations", request.UserId, request.RoleId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_already_has_role"));
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} added to role {RoleId}, {Count} assignment(s) created",
            request.UserId, request.RoleId, added);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_user_added_to_role"));
    }
}
