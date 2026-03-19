using Hangfire.Dashboard;

namespace Nexora.Host;

/// <summary>
/// Allows all access in development environment.
/// </summary>
public sealed class HangfireDevelopmentAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}

/// <summary>
/// Requires the "platform-admin" role for Hangfire dashboard access in production.
/// </summary>
public sealed class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("platform-admin");
    }
}
