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

/// <summary>Query to retrieve all roles assigned to a user within a specific organization.</summary>
public sealed record GetUserRolesQuery(Guid UserId, Guid OrganizationId) : IQuery<List<RoleDto>>;

/// <summary>Handles retrieving a user's roles within an organization, including permission keys.</summary>
public sealed class GetUserRolesHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetUserRolesHandler> logger) : IQueryHandler<GetUserRolesQuery, List<RoleDto>>
{
    public async Task<Result<List<RoleDto>>> Handle(GetUserRolesQuery request, CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);
        var orgId = OrganizationId.From(request.OrganizationId);

        var orgUser = await dbContext.OrganizationUsers
            .AsNoTracking()
            .Include(ou => ou.UserRoles)
            .FirstOrDefaultAsync(ou => ou.UserId == userId && ou.OrganizationId == orgId, ct);

        if (orgUser is null)
        {
            logger.LogDebug("User {UserId} not found in organization {OrgId}", request.UserId, request.OrganizationId);
            return Result<List<RoleDto>>.Failure(
                LocalizedMessage.Of("lockey_identity_error_user_not_in_org"));
        }

        var roleIds = orgUser.UserRoles.Select(ur => ur.RoleId).ToList();

        if (roleIds.Count == 0)
            return Result<List<RoleDto>>.Success([], LocalizedMessage.Of("lockey_identity_user_roles_retrieved"));

        // Load roles with permissions
        var roles = await dbContext.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId)
            .ToListAsync(ct);

        var relevantPermissionIds = roles
            .SelectMany(r => r.Permissions)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToList();
        var allPermissions = await dbContext.Permissions
            .AsNoTracking()
            .Where(p => relevantPermissionIds.Contains(p.Id))
            .ToListAsync(ct);
        var permMap = allPermissions.ToDictionary(p => p.Id, p => p.Key);

        var dtos = roles.Select(r => new RoleDto(
            r.Id.Value, r.Name, r.Description, r.IsSystemRole, r.IsActive,
            r.Permissions
                .Select(rp => permMap.GetValueOrDefault(rp.PermissionId, ""))
                .Where(k => k.Length > 0)
                .ToList(),
            r.CreatedAt
        )).ToList();

        return Result<List<RoleDto>>.Success(dtos, LocalizedMessage.Of("lockey_identity_user_roles_retrieved"));
    }
}
