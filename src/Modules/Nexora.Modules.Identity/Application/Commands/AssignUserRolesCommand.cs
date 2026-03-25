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

/// <summary>Command to assign a set of roles to a user within an organization.</summary>
public sealed record AssignUserRolesCommand(
    Guid UserId,
    Guid OrganizationId,
    List<Guid> RoleIds) : ICommand;

/// <summary>Validates the <see cref="AssignUserRolesCommand"/> inputs.</summary>
public sealed class AssignUserRolesValidator : AbstractValidator<AssignUserRolesCommand>
{
    public AssignUserRolesValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.OrganizationId).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.RoleIds).NotNull().WithMessage("lockey_validation_required");
    }
}

/// <summary>Handles role assignment by reconciling current and requested roles for a user.</summary>
public sealed class AssignUserRolesHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<AssignUserRolesHandler> logger) : ICommandHandler<AssignUserRolesCommand>
{
    public async Task<Result> Handle(AssignUserRolesCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);
        var orgId = OrganizationId.From(request.OrganizationId);

        // Find the OrganizationUser join entry
        var orgUser = await dbContext.OrganizationUsers
            .Include(ou => ou.UserRoles)
            .FirstOrDefaultAsync(ou => ou.UserId == userId && ou.OrganizationId == orgId, ct);

        if (orgUser is null)
        {
            logger.LogWarning("User {UserId} is not a member of organization {OrgId}", request.UserId, request.OrganizationId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_not_in_org"));
        }

        // Validate all requested roles exist in this tenant
        var requestedRoleIds = request.RoleIds.Select(RoleId.From).ToHashSet();
        var validRoles = await dbContext.Roles
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && requestedRoleIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (validRoles.Count != requestedRoleIds.Count)
        {
            logger.LogWarning("Business rule: {Rule} for {Entity} {Id}", "Invalid roles in request", "User", request.UserId);
            return Result.Failure(
                LocalizedMessage.Of("lockey_identity_error_invalid_roles"));
        }

        // Reconcile: remove roles not in request, add new ones
        var currentRoleIds = orgUser.UserRoles.Select(ur => ur.RoleId).ToHashSet();

        var toRemove = orgUser.UserRoles
            .Where(ur => !requestedRoleIds.Contains(ur.RoleId))
            .ToList();

        foreach (var ur in toRemove)
            dbContext.UserRoles.Remove(ur);

        var toAdd = requestedRoleIds.Except(currentRoleIds).ToList();
        foreach (var roleId in toAdd)
            dbContext.UserRoles.Add(UserRole.Create(orgUser.Id, roleId));

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} roles updated in organization {OrgId}: {RoleCount} roles",
            request.UserId, request.OrganizationId, request.RoleIds.Count);

        return Result.Success(LocalizedMessage.Of("lockey_identity_toast_roles_updated"));
    }
}
