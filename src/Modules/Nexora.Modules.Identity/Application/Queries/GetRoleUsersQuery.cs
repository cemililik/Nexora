using System.Diagnostics;
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
    string OrganizationName,
    DateTimeOffset AssignedAt);

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

        var query = (
            from ur in dbContext.UserRoles.AsNoTracking()
            where ur.RoleId == roleId
            from ou in dbContext.OrganizationUsers.AsNoTracking()
            where ou.Id == ur.OrganizationUserId
            from u in dbContext.Users.AsNoTracking()
            where u.Id == ou.UserId && u.TenantId == tenantId
            from o in dbContext.Organizations.AsNoTracking()
            where o.Id == ou.OrganizationId
            orderby u.LastName, u.FirstName
            select new RoleUserDto(
                u.Id.Value,
                u.Email,
                u.FirstName,
                u.LastName,
                o.Id.Value,
                o.Name,
                ur.AssignedAt));

        var sw = Stopwatch.StartNew();

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        sw.Stop();
        if (sw.ElapsedMilliseconds > 500)
        {
            logger.LogWarning("Slow query detected: {QueryName} took {ElapsedMs}ms (RoleId={RoleId}, Page={Page}, PageSize={PageSize})",
                nameof(GetRoleUsersQuery), sw.ElapsedMilliseconds, request.RoleId, request.Page, request.PageSize);
        }

        return Result<PagedResult<RoleUserDto>>.Success(new PagedResult<RoleUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }
}
