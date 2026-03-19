using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Paginated query to list users within the current tenant.</summary>
public sealed record GetUsersQuery(int Page = 1, int PageSize = 20)
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

        var query = dbContext.Users
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
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
        });
    }
}
