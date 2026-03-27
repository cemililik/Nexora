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

/// <summary>Query to get the current user's profile from the JWT subject claim.</summary>
public sealed record GetCurrentUserQuery(string KeycloakUserId) : IQuery<UserDetailDto>;

/// <summary>Resolves the current user from Keycloak ID and returns detail with org memberships.</summary>
public sealed class GetCurrentUserHandler(
    IdentityDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<GetCurrentUserHandler> logger) : IQueryHandler<GetCurrentUserQuery, UserDetailDto>
{
    public async Task<Result<UserDetailDto>> Handle(
        GetCurrentUserQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantId.Parse(tenantContextAccessor.Current.TenantId);

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == request.KeycloakUserId && u.TenantId == tenantId,
                cancellationToken);

        if (user is null)
        {
            logger.LogDebug("User not found for KeycloakUserId {KeycloakUserId} in tenant {TenantId}", request.KeycloakUserId, tenantId);
            return Result<UserDetailDto>.Failure(LocalizedMessage.Of("lockey_identity_error_user_not_found"));
        }

        var orgs = await dbContext.OrganizationUsers.AsNoTracking()
            .Where(ou => ou.UserId == user.Id)
            .Join(dbContext.Organizations,
                ou => ou.OrganizationId,
                o => o.Id,
                (ou, o) => new UserOrganizationDto(o.Id.Value, o.Name, ou.IsDefaultOrg))
            .ToListAsync(cancellationToken);

        var dto = new UserDetailDto(
            user.Id.Value, user.Email, user.FirstName, user.LastName,
            user.Phone, user.Status.ToString(), user.LastLoginAt, orgs);

        return Result<UserDetailDto>.Success(dto,
            LocalizedMessage.Of("lockey_identity_user_retrieved"));
    }
}
