using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.SharedKernel.Abstractions.Secrets;

namespace Nexora.Modules.Identity.Infrastructure.Keycloak;

/// <summary>Keycloak Admin REST API client with automatic token management.</summary>
public sealed class KeycloakAdminService(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options,
    ISecretProvider secretProvider,
    ILogger<KeycloakAdminService> logger) : IKeycloakAdminService
{
    private readonly KeycloakOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <inheritdoc />
    public async Task<string> CreateRealmAsync(string realmName, string displayName, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var realm = new KeycloakRealmRepresentation
        {
            Realm = realmName,
            DisplayName = displayName,
            Enabled = true
        };

        var response = await httpClient.PostAsJsonAsync("/admin/realms", realm, ct);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogWarning("Realm {RealmName} already exists in Keycloak", realmName);
            return realmName;
        }

        response.EnsureSuccessStatusCode();
        logger.LogInformation("Created Keycloak realm {RealmName}", realmName);
        return realmName;
    }

    /// <inheritdoc />
    public async Task<string> CreateUserAsync(string realm, string username, string email,
        string firstName, string lastName, string temporaryPassword, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var user = new KeycloakUserRepresentation
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Enabled = true,
            EmailVerified = false,
            Credentials =
            [
                new KeycloakCredential
                {
                    Type = "password",
                    Value = temporaryPassword,
                    Temporary = true
                }
            ]
        };

        var response = await httpClient.PostAsJsonAsync($"/admin/realms/{realm}/users", user, ct);
        response.EnsureSuccessStatusCode();

        // Keycloak returns the user ID in the Location header
        var locationHeader = response.Headers.Location?.ToString();
        var keycloakUserId = locationHeader?.Split('/').Last()
            ?? throw new InvalidOperationException("Keycloak did not return a user ID in the Location header.");

        logger.LogInformation("Created Keycloak user {Username} in realm {Realm} with ID {KeycloakUserId}",
            username, realm, keycloakUserId);

        return keycloakUserId;
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(string realm, string keycloakUserId, string email,
        string firstName, string lastName, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var user = new KeycloakUserRepresentation
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        var response = await httpClient.PutAsJsonAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}", user, ct);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Updated Keycloak user {KeycloakUserId} in realm {Realm}", keycloakUserId, realm);
    }

    /// <inheritdoc />
    public async Task DisableUserAsync(string realm, string keycloakUserId, CancellationToken ct = default)
    {
        await SetUserEnabledAsync(realm, keycloakUserId, false, ct);
    }

    /// <inheritdoc />
    public async Task EnableUserAsync(string realm, string keycloakUserId, CancellationToken ct = default)
    {
        await SetUserEnabledAsync(realm, keycloakUserId, true, ct);
    }

    private async Task SetUserEnabledAsync(string realm, string keycloakUserId, bool enabled, CancellationToken ct)
    {
        await EnsureAuthenticatedAsync(ct);

        // GET the full user representation first — Keycloak PUT requires the complete object
        var getUserResponse = await httpClient.GetAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}", ct);
        getUserResponse.EnsureSuccessStatusCode();

        var user = await getUserResponse.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(ct)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize Keycloak user {keycloakUserId} from realm {realm}.");

        var updatedUser = user with { Enabled = enabled };

        var response = await httpClient.PutAsJsonAsync(
            $"/admin/realms/{realm}/users/{keycloakUserId}", updatedUser, ct);
        response.EnsureSuccessStatusCode();

        logger.LogInformation("Set Keycloak user {KeycloakUserId} enabled={Enabled} in realm {Realm}",
            keycloakUserId, enabled, realm);
    }

    /// <summary>
    /// Acquires a valid admin token, refreshing if expired. All access is serialized
    /// through the lock to prevent race conditions on token refresh and header mutation.
    /// </summary>
    private async Task EnsureAuthenticatedAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            // Check inside lock — another thread may have refreshed while we waited
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry)
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _cachedToken);
                return;
            }

            var adminUsername = await secretProvider.GetSecretAsync("nexora/keycloak/admin-username", ct);
            var adminPassword = await secretProvider.GetSecretAsync("nexora/keycloak/admin-password", ct);

            var tokenUrl = $"/realms/{_options.AdminRealm}/protocol/openid-connect/token";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = _options.AdminClientId,
                ["username"] = adminUsername,
                ["password"] = adminPassword
            });

            var response = await httpClient.PostAsync(tokenUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct)
                ?? throw new InvalidOperationException("Failed to deserialize Keycloak token response.");

            _cachedToken = token.AccessToken;
            // Expire 30 seconds early to avoid edge cases
            _tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 30);

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _cachedToken);

            logger.LogDebug("Obtained Keycloak admin token, expires in {ExpiresIn}s", token.ExpiresIn);
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
