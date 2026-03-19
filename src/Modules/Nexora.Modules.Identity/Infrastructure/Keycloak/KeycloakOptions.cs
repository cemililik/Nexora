namespace Nexora.Modules.Identity.Infrastructure.Keycloak;

/// <summary>Strongly-typed configuration for Keycloak Admin API connection.</summary>
public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>Base URL of the Keycloak server (e.g. http://localhost:8080).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Admin realm used for obtaining admin access tokens.</summary>
    public string AdminRealm { get; set; } = "master";

    /// <summary>Client ID for admin API access.</summary>
    public string AdminClientId { get; set; } = "admin-cli";
}
