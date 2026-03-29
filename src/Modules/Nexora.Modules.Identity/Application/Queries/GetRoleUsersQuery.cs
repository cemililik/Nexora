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

/// <summary>Query to retrieve users assigned to a specific role.</summary>
public sealed record GetRoleUsersQuery(
    Guid RoleId,
    int Page = 1,
    int PageSize = 20)
    : IQuery<PagedResult<RoleUserDto>>;

/// <summary>User assigned to a role with assignment context.</summary>
public sealed record RoleUserDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid OrganizationId,
    string OrganizationName);

/// <summary>Handles retrieving users assigned to a specific role.</summary>
public sealed class GetRoleUsersHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetRoleUsersHandler> logger) : IQueryHandler<GetRoleUsersQuery, PagedResult<RoleUserDto>>
{
    public async Task<Result<PagedResult<RoleUserDto>>> Handle(
        GetRoleUsersQuery request,
        CancellationToken ct)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var roleId = RoleId.From(request.RoleId);

        // Verify role exists
        var roleExists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id == roleId && r.TenantId == tenantId, ct);

        if (!roleExists)
        {
            logger.LogDebug("Role {RoleId} not found for tenant {TenantId}", request.RoleId, tenantId);
            return Result<PagedResult<RoleUserDto>>.Failure(
                LocalizedMessage.Of("lockey_identity_error_role_not_found"));
        }

        var query = dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Join(
                dbContext.OrganizationUsers.AsNoTracking(),
                ur => ur.OrganizationUserId,
                ou => ou.Id,
                (ur, ou) => new { ur, ou })
            .Join(
                dbContext.Users.AsNoTracking().Where(u => u.TenantId == tenantId),
                x => x.ou.UserId,
                u => u.Id,
                (x, u) => new { x.ou, User = u })
            .Join(
                dbContext.Organizations.AsNoTracking(),
                x => x.ou.OrganizationId,
                o => o.Id,
                (x, o) => new RoleUserDto(
                    x.User.Id.Value,
                    x.User.Email,
                    x.User.FirstName,
                    x.User.LastName,
                    o.Id.Value,
                    o.Name))
            .OrderBy(r => r.LastName)
            .ThenBy(r => r.FirstName);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return Result<PagedResult<RoleUserDto>>.Success(new PagedResult<RoleUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
