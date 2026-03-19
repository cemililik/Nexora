namespace Nexora.Modules.Identity.Infrastructure.Keycloak;

/// <summary>Abstraction for Keycloak Admin REST API operations.</summary>
public interface IKeycloakAdminService
{
    /// <summary>Creates a new realm for a tenant. Returns the realm name.</summary>
    Task<string> CreateRealmAsync(string realmName, string displayName, CancellationToken ct = default);

    /// <summary>Creates a user in the specified realm. Returns the Keycloak user ID.</summary>
    Task<string> CreateUserAsync(string realm, string username, string email,
        string firstName, string lastName, string temporaryPassword, CancellationToken ct = default);

    /// <summary>Updates an existing user's profile in the specified realm.</summary>
    Task UpdateUserAsync(string realm, string keycloakUserId, string email,
        string firstName, string lastName, CancellationToken ct = default);

    /// <summary>Disables a user in the specified realm.</summary>
    Task DisableUserAsync(string realm, string keycloakUserId, CancellationToken ct = default);

    /// <summary>Enables a user in the specified realm.</summary>
    Task EnableUserAsync(string realm, string keycloakUserId, CancellationToken ct = default);
}
