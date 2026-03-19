using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to list all roles with their permissions for the current tenant.</summary>
public sealed record GetRolesQuery : IQuery<List<RoleDto>>;

/// <summary>Returns roles with resolved permission keys for the current tenant.</summary>
public sealed class GetRolesHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetRolesQuery, List<RoleDto>>
{
    public async Task<Result<List<RoleDto>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var roles = await dbContext.Roles
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var allPermissions = await dbContext.Permissions.ToListAsync(cancellationToken);
        var permissionMap = allPermissions.ToDictionary(p => p.Id, p => p.Key);

        var dtos = roles.Select(r => new RoleDto(
            r.Id.Value,
            r.Name,
            r.Description,
            r.IsSystemRole,
            r.IsActive,
            r.Permissions
                .Select(rp => permissionMap.GetValueOrDefault(rp.PermissionId, ""))
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList()
        )).ToList();

        return Result<List<RoleDto>>.Success(dtos,
            new LocalizedMessage("lockey_identity_roles_listed"));
    }
}
