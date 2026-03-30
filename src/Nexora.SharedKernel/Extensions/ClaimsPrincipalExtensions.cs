using System.Security.Claims;

namespace Nexora.SharedKernel.Extensions;

/// <summary>
/// Typed extension methods for accessing Keycloak JWT claims.
/// Centralizes claim key strings to avoid magic strings throughout the codebase.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string SubClaim = "sub";
    private const string EmailClaim = "email";
    private const string TenantIdClaim = "tenant_id";
    private const string OrganizationIdClaim = "organization_id";

    /// <summary>Gets the Keycloak user ID from the JWT claims (NameIdentifier or sub).</summary>
    public static string? GetKeycloakUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? principal.FindFirstValue(SubClaim);

    /// <summary>Gets the user's email address from the JWT claims.</summary>
    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(EmailClaim);

    /// <summary>Gets the tenant ID from the JWT claims.</summary>
    public static string? GetTenantId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(TenantIdClaim);

    /// <summary>Gets the organization ID from the JWT claims.</summary>
    public static string? GetOrganizationId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(OrganizationIdClaim);
}
