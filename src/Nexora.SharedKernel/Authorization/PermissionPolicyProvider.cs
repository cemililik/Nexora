using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Nexora.SharedKernel.Authorization;

/// <summary>
/// Dynamically creates authorization policies for permission strings matching
/// the <c>{module}.{resource}.{action}</c> pattern. Falls back to the default
/// provider for standard policies (e.g., the unnamed default policy).
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (IsPermissionPolicy(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        // Default policy (bare .RequireAuthorization()) = authenticated user only
        return _fallback.GetDefaultPolicyAsync();
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallback.GetFallbackPolicyAsync();
    }

    /// <summary>
    /// Returns <c>true</c> when the policy name looks like a permission string:
    /// at least two dots separating module, resource, and action segments.
    /// </summary>
    private static bool IsPermissionPolicy(string policyName)
    {
        // Permission format: {module}.{resource}.{action} — must have at least 2 dots
        var dotCount = 0;
        foreach (var ch in policyName)
        {
            if (ch == '.')
                dotCount++;
            if (dotCount >= 2)
                return true;
        }

        return false;
    }
}
