using Microsoft.AspNetCore.Http;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Extensions;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Infrastructure.MultiTenancy;

/// <summary>
/// Extracts tenant context from JWT claims and sets it for the request.
/// Returns 401 if the user is authenticated but tenant_id claim is missing.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    // Hardcoded for simplicity — move to configuration if list grows
    private static readonly HashSet<string> _publicPaths =
    [
        "/health",
        "/admin/hangfire"
    ];

    /// <summary>Extracts tenant context from JWT claims and sets it for the current request.</summary>
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor accessor)
    {
        // Skip tenant resolution for public/infrastructure endpoints (health, Hangfire)
        if (_publicPaths.Any(p => context.Request.Path.StartsWithSegments(new PathString(p), StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var tenantId = context.User.GetTenantId();
        var orgId = context.User.GetOrganizationId();
        var userId = context.User.GetKeycloakUserId();

        if (!string.IsNullOrEmpty(tenantId))
        {
            accessor.SetTenant(tenantId, orgId, userId);
        }
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            // Authenticated user without tenant claim — reject
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                ApiEnvelope<object>.Fail(new Error(LocalizedMessage.Of("lockey_error_tenant_context_missing"))));
            return;
        }

        await next(context);
    }
}
