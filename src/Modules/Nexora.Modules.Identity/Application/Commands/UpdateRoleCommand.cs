using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to update a role's name, description, and permissions.</summary>
public sealed record UpdateRoleCommand(
    Guid Id,
    string Name,
    string? Description,
    List<Guid>? PermissionIds) : ICommand<RoleDto>;

/// <summary>Validates the <see cref="UpdateRoleCommand"/> inputs.</summary>
public sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("lockey_validation_required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("lockey_validation_required")
            .MaximumLength(100).WithMessage("lockey_validation_max_length");
    }
}

/// <summary>Handles updating a role's details and reconciling its permissions.</summary>
public sealed class UpdateRoleHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<UpdateRoleHandler> logger) : ICommandHandler<UpdateRoleCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.Id);

        var role = await dbContext.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

        if (role is null)
        {
            logger.LogWarning("Role {RoleId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<RoleDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_not_found"));
        }

        if (role.IsSystemRole)
        {
            logger.LogWarning("Cannot modify system role. RoleId {RoleId} for tenant {TenantId}", request.Id, tenantId);
            return Result<RoleDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_system_role_immutable"));
        }

        // Check name uniqueness (excluding self)
        var nameExists = await dbContext.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == request.Name && r.Id != roleId, ct);

        if (nameExists)
        {
            logger.LogWarning("Role name {RoleName} already exists for tenant {TenantId}", request.Name, tenantId);
            return Result<RoleDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_name_taken"));
        }

        role.Update(request.Name, request.Description);

        // Reconcile permissions
        if (request.PermissionIds is not null)
        {
            var requestedIds = request.PermissionIds.Select(PermissionId.From).ToHashSet();
            var currentIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();

            // Revoke removed
            var toRevoke = currentIds.Except(requestedIds).ToList();
            foreach (var permId in toRevoke)
                role.RevokePermission(permId);

            // Assign new
            var toAssign = requestedIds.Except(currentIds).ToList();
            if (toAssign.Count > 0)
            {
                var permissions = await dbContext.Permissions
                    .Where(p => toAssign.Contains(p.Id))
                    .ToListAsync(ct);

                foreach (var perm in permissions)
                    role.AssignPermission(perm);
            }
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Role {RoleId} updated for tenant {TenantId}", role.Id, tenantId);

        // Build permission keys
        var rolePermissionIds = role.Permissions.Select(rp => rp.PermissionId).ToList();
        var allPermissions = await dbContext.Permissions
            .Where(p => rolePermissionIds.Contains(p.Id))
            .ToListAsync(ct);
        var permMap = allPermissions.ToDictionary(p => p.Id, p => p.Key);
        var permKeys = role.Permissions
            .Select(rp => permMap.GetValueOrDefault(rp.PermissionId, ""))
            .Where(k => k.Length > 0)
            .ToList();

        return Result<RoleDto>.Success(
            new RoleDto(role.Id.Value, role.Name, role.Description, role.IsSystemRole, role.IsActive, permKeys, role.CreatedAt),
            LocalizedMessage.Of("lockey_identity_toast_role_updated"));
    }
}
