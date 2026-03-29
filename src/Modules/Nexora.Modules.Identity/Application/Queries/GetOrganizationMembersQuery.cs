using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to list members of an organization with pagination.</summary>
public sealed record GetOrganizationMembersQuery(
    Guid OrganizationId,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<OrganizationMemberDto>>;

/// <summary>Returns paginated members of an organization.</summary>
public sealed class GetOrganizationMembersHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetOrganizationMembersQuery, PagedResult<OrganizationMemberDto>>
{
    public async Task<Result<PagedResult<OrganizationMemberDto>>> Handle(
        GetOrganizationMembersQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var orgId = OrganizationId.From(request.OrganizationId);

        // Verify organization belongs to tenant
        var orgExists = await dbContext.Organizations
            .AnyAsync(o => o.Id == orgId && o.TenantId == tenantId, cancellationToken);

        if (!orgExists)
            return Result<PagedResult<OrganizationMemberDto>>.Failure("lockey_identity_error_org_not_found");

        var query = dbContext.OrganizationUsers
            .Where(ou => ou.OrganizationId == orgId)
            .Join(dbContext.Users,
                ou => ou.UserId,
                u => u.Id,
                (ou, u) => new { ou, u });

        var totalCount = await query.CountAsync(cancellationToken);

        var members = await query
            .OrderBy(x => x.u.LastName).ThenBy(x => x.u.FirstName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new OrganizationMemberDto(
                x.u.Id.Value,
                x.u.Email,
                x.u.FirstName,
                x.u.LastName,
                x.ou.IsDefaultOrg,
                x.ou.JoinedAt))
            .ToListAsync(cancellationToken);

        var result = new PagedResult<OrganizationMemberDto>
        {
            TotalCount = totalCount,
            Items = members,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<OrganizationMemberDto>>.Success(result,
            new LocalizedMessage("lockey_identity_org_members_listed"));
    }
}
