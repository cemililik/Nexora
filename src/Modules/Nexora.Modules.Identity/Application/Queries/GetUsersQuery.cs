using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Paginated query to list users within the current tenant.</summary>
public sealed record GetUsersQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? OrganizationId = null,
    Guid? RoleId = null,
    string? Search = null)
    : IQuery<PagedResult<UserDto>>;

/// <summary>Returns a paginated list of users filtered by tenant context.</summary>
public sealed class GetUsersHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetUsersQuery, PagedResult<UserDto>>
{
    public async Task<Result<PagedResult<UserDto>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var query = dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId);

        if (request.OrganizationId.HasValue)
        {
            var orgId = OrganizationId.From(request.OrganizationId.Value);
            query = query.Where(u =>
                dbContext.OrganizationUsers.Any(ou =>
                    ou.UserId == u.Id && ou.OrganizationId == orgId));
        }

        if (request.RoleId.HasValue)
        {
            var roleId = Domain.ValueObjects.RoleId.From(request.RoleId.Value);
            query = query.Where(u =>
                dbContext.OrganizationUsers.Any(ou =>
                    ou.UserId == u.Id &&
                    dbContext.UserRoles.Any(ur =>
                        ur.OrganizationUserId == ou.Id && ur.RoleId == roleId)));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search));
        }

        var orderedQuery = query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserDto(
                u.Id.Value,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Phone,
                u.Status.ToString(),
                u.LastLoginAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<UserDto>>.Success(new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        }, new LocalizedMessage("lockey_identity_users_listed"));
    }
}
