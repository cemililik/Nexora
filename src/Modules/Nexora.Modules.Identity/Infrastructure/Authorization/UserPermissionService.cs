using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Abstractions.Caching;
using Nexora.SharedKernel.Authorization;

namespace Nexora.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Loads user permissions from the Identity module's database via the
/// OrganizationUser -> UserRole -> RolePermission -> Permission chain.
/// Results are cached for 5 minutes via <see cref="ICacheService"/>.
/// </summary>
public sealed class UserPermissionService(
    IdentityDbContext dbContext,
    ICacheService cacheService,
    ILogger<UserPermissionService> logger) : IUserPermissionService
{
    private static readonly CacheOptions PermissionCacheOptions = new()
    {
        L1Ttl = TimeSpan.FromMinutes(2),
        L2Ttl = TimeSpan.FromMinutes(5)
    };

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetUserPermissionsAsync(
        string tenantId,
        string keycloakUserId,
        CancellationToken ct = default)
    {
        var cacheKey = $"auth:permissions:{keycloakUserId}";

        var cached = await cacheService.GetOrSetAsync(
            cacheKey,
            async token => await LoadPermissionsFromDbAsync(keycloakUserId, token),
            PermissionCacheOptions,
            ct);

        return cached;
    }

    private async Task<HashSet<string>> LoadPermissionsFromDbAsync(
        string keycloakUserId,
        CancellationToken ct)
    {
        logger.LogDebug("Loading permissions from DB for user {KeycloakUserId}", keycloakUserId);

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakUserId == keycloakUserId, ct);

        if (user is null)
        {
            logger.LogDebug("User {KeycloakUserId} not found in tenant — returning empty permissions", keycloakUserId);
            return [];
        }

        var permissions = await dbContext.OrganizationUsers.AsNoTracking()
            .Where(ou => ou.UserId == user.Id)
            .Join(dbContext.UserRoles, ou => ou.Id, ur => ur.OrganizationUserId, (_, ur) => ur.RoleId)
            .Join(dbContext.RolePermissions, roleId => roleId, rp => rp.RoleId, (_, rp) => rp.PermissionId)
            .Join(dbContext.Permissions, permId => permId, p => p.Id,
                (_, p) => p.Module + "." + p.Resource + "." + p.Action)
            .Distinct()
            .ToListAsync(ct);

        logger.LogDebug("Loaded {Count} permissions for user {KeycloakUserId}", permissions.Count, keycloakUserId);

        return [.. permissions];
    }
}
