using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.Entities;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
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

        // Assign permissions if provided
        if (request.PermissionIds is { Count: > 0 })
        {
            var permissionIds = request.PermissionIds.Select(PermissionId.From).ToList();
            var permissions = await dbContext.Permissions
                .Where(p => permissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var permission in permissions)
                role.AssignPermission(permission);
        }

        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var permissionKeys = role.Permissions
            .Select(rp => dbContext.Permissions.Find(rp.PermissionId)?.Key ?? "")
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

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
