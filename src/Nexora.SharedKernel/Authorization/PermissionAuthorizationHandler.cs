using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexora.SharedKernel.Extensions;

namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Evaluates <see cref="PermissionRequirement"/> by loading the user's effective permissions
/// via <see cref="IUserPermissionService"/> and checking for a match.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IUserPermissionService permissionService,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var keycloakUserId = context.User.GetKeycloakUserId();
        if (string.IsNullOrEmpty(keycloakUserId))
        {
            logger.LogDebug("Permission check skipped — no Keycloak user ID in claims");
            return;
        }

        var tenantId = context.User.GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            logger.LogDebug("Permission check skipped — no tenant ID in claims for user {KeycloakUserId}", keycloakUserId);
            return;
        }

        var ct = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        var permissions = await permissionService.GetUserPermissionsAsync(
            tenantId, keycloakUserId, ct);

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Permission denied — user {KeycloakUserId} lacks {Permission} in tenant {TenantId}",
                keycloakUserId, requirement.Permission, tenantId);
        }
    }
}
