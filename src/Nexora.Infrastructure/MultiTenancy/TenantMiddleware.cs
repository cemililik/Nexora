using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nexora.SharedKernel.Abstractions.MultiTenancy;
using Nexora.SharedKernel.Localization;
using Nexora.SharedKernel.Results;

namespace Nexora.Infrastructure.MultiTenancy;

/// <summary>
/// Extracts tenant context from JWT claims and sets it for the request.
/// Returns 401 if the user is authenticated but tenant_id claim is missing.
/// </summary>
public sealed class TenantMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> _publicPaths =
    [
        "/health",
        "/admin/hangfire"
    ];

    /// <summary>Extracts tenant context from JWT claims and sets it for the current request.</summary>
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor accessor)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip tenant resolution for public endpoints
        if (_publicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var tenantId = context.User.FindFirstValue("tenant_id");
        var orgId = context.User.FindFirstValue("organization_id");
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

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
