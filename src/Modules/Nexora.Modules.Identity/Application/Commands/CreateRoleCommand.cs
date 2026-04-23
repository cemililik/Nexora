using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Authorization;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Commands;

/// <summary>Command to create a role with optional permission assignments.</summary>
public sealed record CreateRoleCommand(
    string Name,
    string? Description,
    List<Guid>? PermissionIds) : ICommand<RoleDto>;

/// <summary>Validates role creation input (name required, max length).</summary>
public sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("lockey_identity_validation_role_name_required")
            .MaximumLength(100).WithMessage("lockey_identity_validation_role_name_max_length");
    }
}

/// <summary>Creates a role and assigns requested permissions within the tenant.</summary>
public sealed class CreateRoleHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<CreateRoleHandler> logger) : ICommandHandler<CreateRoleCommand, RoleDto>
{
    public async Task<Result<RoleDto>> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var nameExists = await dbContext.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == request.Name, cancellationToken);

        if (nameExists)
        {
            logger.LogWarning("Role creation failed: name {RoleName} already taken for tenant {TenantId}", request.Name, tenantId);
            return Result<RoleDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_name_taken",
                new Dictionary<string, string> { ["name"] = request.Name }));
        }

        var role = Role.Create(tenantId, request.Name, request.Description);

        // Assign permissions if provided — keep reference for DTO mapping below
        var loadedPermissions = new List<Permission>();
        if (request.PermissionIds is { Count: > 0 })
        {
            var permissionIds = request.PermissionIds.Select(PermissionId.From).ToList();
            loadedPermissions = await dbContext.Permissions
                .Where(p => permissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            // Platform-scope permissions can never be assigned to tenant roles.
            var platformPerm = loadedPermissions.FirstOrDefault(p => p.Scope == PermissionScope.Platform);
            if (platformPerm is not null)
            {
                logger.LogWarning(
                    "Role creation rejected: platform-scope permission {PermissionKey} cannot be assigned to a tenant role",
                    platformPerm.Key);
                return Result<RoleDto>.Failure(
                    LocalizedMessage.Of("lockey_identity_error_platform_permission_denied"));
            }

            foreach (var permission in loadedPermissions)
                role.AssignPermission(permission);
        }

        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Use the already-loaded permissions list instead of a second DB query
        var permissionKeys = loadedPermissions.Select(p => p.Key).ToList();

        var dto = new RoleDto(
            role.Id.Value,
            role.Name,
            role.Description,
            role.IsSystemRole,
            role.IsActive,
            permissionKeys,
            role.CreatedAt);

        logger.LogInformation("Role {RoleId} created for tenant {TenantId}", role.Id, tenantId);

        return Result<RoleDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_role_created"));
    }
}
