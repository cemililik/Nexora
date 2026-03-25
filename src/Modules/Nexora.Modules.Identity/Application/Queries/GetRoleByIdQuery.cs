using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to retrieve a role by its identifier, including permissions and assigned user count.</summary>
public sealed record GetRoleByIdQuery(Guid Id) : IQuery<RoleDetailDto>;

/// <summary>Extended role DTO with permission details and assigned user count.</summary>
public sealed record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    bool IsActive,
    List<PermissionDto> Permissions,
    int AssignedUserCount);

/// <summary>Handles retrieving a single role with its permission details and assigned user count.</summary>
public sealed class GetRoleByIdHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetRoleByIdHandler> logger) : IQueryHandler<GetRoleByIdQuery, RoleDetailDto>
{
    public async Task<Result<RoleDetailDto>> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.Id);

        var role = await dbContext.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

        if (role is null)
        {
            logger.LogDebug("Role {RoleId} not found for tenant {TenantId}", request.Id, tenantId);
            return Result<RoleDetailDto>.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_not_found"));
        }

        // Load permission details
        var permissionIds = role.Permissions.Select(rp => rp.PermissionId).ToList();
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => new PermissionDto(p.Id.Value, p.Module, p.Resource, p.Action, p.Key, p.Description))
            .ToListAsync(ct);

        // Count assigned users
        var assignedUserCount = await dbContext.UserRoles
            .AsNoTracking()
            .CountAsync(ur => ur.RoleId == roleId, ct);

        return Result<RoleDetailDto>.Success(
            new RoleDetailDto(
                role.Id.Value, role.Name, role.Description,
                role.IsSystemRole, role.IsActive,
                permissions, assignedUserCount));
    }
}
