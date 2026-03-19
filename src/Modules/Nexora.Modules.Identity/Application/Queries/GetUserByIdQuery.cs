using Microsoft.EntityFrameworkCore;
using Nexora.Modules.Identity.Application.DTOs;
using Nexora.Modules.Identity.Domain.ValueObjects;
using Nexora.Modules.Identity.Infrastructure;
using Nexora.SharedKernel.Abstractions.CQRS;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Modules.Identity.Application.Queries;

/// <summary>Query to get user detail by ID with organization memberships.</summary>
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDetailDto>;

/// <summary>Returns user detail with organization memberships for the current tenant.</summary>
public sealed class GetUserByIdHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : IQueryHandler<GetUserByIdQuery, UserDetailDto>
{
    public async Task<Result<UserDetailDto>> Handle(
        GetUserByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);
        var userId = UserId.From(request.UserId);

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, cancellationToken);

        if (user is null)
            return Result<UserDetailDto>.Failure("lockey_identity_error_user_not_found");

        var orgs = await dbContext.OrganizationUsers
            .Where(ou => ou.UserId == userId)
            .Join(dbContext.Organizations,
                ou => ou.OrganizationId,
                o => o.Id,
                (ou, o) => new UserOrganizationDto(o.Id.Value, o.Name, ou.IsDefaultOrg))
            .ToListAsync(cancellationToken);

        var dto = new UserDetailDto(
            user.Id.Value, user.Email, user.FirstName, user.LastName,
            user.Phone, user.Status.ToString(), user.LastLoginAt, orgs);

        return Result<UserDetailDto>.Success(dto,
            new LocalizedMessage("lockey_identity_user_retrieved"));
    }
}
