using System.Diagnostics;
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
    private static readonly ActivitySource ActivitySource = new("Nexora.Identity.Keycloak");

    private readonly KeycloakOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public async Task<string> CreateRealmAsync(string realmName, string displayName, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("Keycloak.CreateRealm", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", "CreateRealm");
        activity?.SetTag("keycloak.realm", realmName);

        Exception? capturedException = null;
        try
        {
            var token = await EnsureAuthenticatedAsync(ct);

            var realm = new KeycloakRealmRepresentation
            {
                Realm = realmName,
                DisplayName = displayName,
                Enabled = true
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/admin/realms");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(realm);

            var response = await httpClient.SendAsync(request, ct);
            activity?.SetTag("http.status_code", (int)response.StatusCode);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogWarning("Realm {RealmName} already exists in Keycloak", realmName);
                return realmName;
            }

            response.EnsureSuccessStatusCode();
            logger.LogInformation("Created Keycloak realm {RealmName}", realmName);
            return realmName;
        }
        catch (HttpRequestException ex)
        {
            capturedException = ex;
            throw;
        }
        catch (JsonException ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            if (capturedException is not null)
                activity?.SetStatus(ActivityStatusCode.Error, capturedException.Message);
        }
    }

    /// <inheritdoc />
    public async Task DeleteRealmAsync(string realmName, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("Keycloak.DeleteRealm", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", "DeleteRealm");
        activity?.SetTag("keycloak.realm", realmName);

        var token = await EnsureAuthenticatedAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/admin/realms/{Uri.EscapeDataString(realmName)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, ct);
        activity?.SetTag("http.status_code", (int)response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Realm {RealmName} not found during deletion (already removed)", realmName);
            return;
        }

        response.EnsureSuccessStatusCode();
        logger.LogInformation("Deleted Keycloak realm {RealmName}", realmName);
    }

    /// <inheritdoc />
    public async Task<string> CreateUserAsync(string realm, string username, string email,
        string firstName, string lastName, string temporaryPassword, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("Keycloak.CreateUser", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", "CreateUser");
        activity?.SetTag("keycloak.realm", realm);

        Exception? capturedException = null;
        try
        {
            var token = await EnsureAuthenticatedAsync(ct);

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

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/realms/{Uri.EscapeDataString(realm)}/users");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(user);

            var response = await httpClient.SendAsync(request, ct);
            activity?.SetTag("http.status_code", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            // Keycloak returns the user ID in the Location header
            var locationHeader = response.Headers.Location?.ToString();
            var keycloakUserId = locationHeader?.Split('/').Last()
                ?? throw new KeycloakIntegrationException("lockey_identity_keycloak_missing_location_header",
                    new() { ["realm"] = realm });

            if (string.IsNullOrWhiteSpace(keycloakUserId))
            {
                throw new KeycloakIntegrationException("lockey_identity_keycloak_missing_location_header",
                    new() { ["realm"] = realm });
            }

            logger.LogInformation("Created Keycloak user in realm {Realm}", realm);

            return keycloakUserId;
        }
        catch (HttpRequestException ex)
        {
            capturedException = ex;
            throw;
        }
        catch (JsonException ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            if (capturedException is not null)
                activity?.SetStatus(ActivityStatusCode.Error, capturedException.Message);
        }
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(string realm, string keycloakUserId, string email,
        string firstName, string lastName, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("Keycloak.UpdateUser", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", "UpdateUser");
        activity?.SetTag("keycloak.realm", realm);

        Exception? capturedException = null;
        try
        {
            var token = await EnsureAuthenticatedAsync(ct);

            // GET the full user representation first — Keycloak PUT requires the complete object
            using var getRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var getUserResponse = await httpClient.SendAsync(getRequest, ct);
            getUserResponse.EnsureSuccessStatusCode();

            var user = await getUserResponse.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(ct)
                ?? throw new KeycloakIntegrationException("lockey_identity_keycloak_deserialize_failed");

            var updatedUser = user with
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName
            };

            using var putRequest = new HttpRequestMessage(HttpMethod.Put,
                $"/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
            putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            putRequest.Content = JsonContent.Create(updatedUser);

            var response = await httpClient.SendAsync(putRequest, ct);
            activity?.SetTag("http.status_code", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Updated Keycloak user in realm {Realm}", realm);
        }
        catch (HttpRequestException ex)
        {
            capturedException = ex;
            throw;
        }
        catch (JsonException ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            if (capturedException is not null)
                activity?.SetStatus(ActivityStatusCode.Error, capturedException.Message);
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string realm, string keycloakUserId, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("Keycloak.DeleteUser", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", "DeleteUser");
        activity?.SetTag("keycloak.realm", realm);

        var token = await EnsureAuthenticatedAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await httpClient.SendAsync(request, ct);
        activity?.SetTag("http.status_code", (int)response.StatusCode);

        // Idempotent compensation: a 404 means the user is already absent — treat as success so
        // compensation flows (e.g. CreateUserCommand rollback) do not surface spurious failures.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Keycloak user {KeycloakUserId} not found in realm {Realm} during deletion (already removed)",
                keycloakUserId, realm);
            return;
        }

        response.EnsureSuccessStatusCode();
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
        var operationName = enabled ? "EnableUser" : "DisableUser";
        using var activity = ActivitySource.StartActivity($"Keycloak.{operationName}", ActivityKind.Client);
        activity?.SetTag("keycloak.operation", operationName);
        activity?.SetTag("keycloak.realm", realm);

        Exception? capturedException = null;
        try
        {
            var token = await EnsureAuthenticatedAsync(ct);

            // GET the full user representation first — Keycloak PUT requires the complete object
            using var getRequest = new HttpRequestMessage(HttpMethod.Get,
                $"/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
            getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var getUserResponse = await httpClient.SendAsync(getRequest, ct);
            getUserResponse.EnsureSuccessStatusCode();

            var user = await getUserResponse.Content.ReadFromJsonAsync<KeycloakUserRepresentation>(ct)
                ?? throw new KeycloakIntegrationException("lockey_identity_keycloak_deserialize_failed");

            var updatedUser = user with { Enabled = enabled };

            using var putRequest = new HttpRequestMessage(HttpMethod.Put,
                $"/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(keycloakUserId)}");
            putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            putRequest.Content = JsonContent.Create(updatedUser);

            var response = await httpClient.SendAsync(putRequest, ct);
            activity?.SetTag("http.status_code", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Set Keycloak user enabled={Enabled} in realm {Realm}",
                enabled, realm);
        }
        catch (HttpRequestException ex)
        {
            capturedException = ex;
            throw;
        }
        catch (JsonException ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            if (capturedException is not null)
                activity?.SetStatus(ActivityStatusCode.Error, capturedException.Message);
        }
    }

    /// <summary>
    /// Acquires a valid admin token, refreshing if expired.
    /// Returns the token string for use in per-request Authorization headers.
    /// </summary>
    private async Task<string> EnsureAuthenticatedAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            // Token still valid — return cached token
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            using var activity = ActivitySource.StartActivity("Keycloak.GetToken", ActivityKind.Client);
            activity?.SetTag("keycloak.operation", "GetToken");
            activity?.SetTag("keycloak.realm", _options.AdminRealm);

            Exception? capturedException = null;
            try
            {
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
                activity?.SetTag("http.status_code", (int)response.StatusCode);
                response.EnsureSuccessStatusCode();

                var tokenResponse = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(ct)
                    ?? throw new KeycloakIntegrationException("lockey_identity_keycloak_token_deserialize_failed");

                _cachedToken = tokenResponse.AccessToken;
                // Expire 30 seconds early to avoid edge cases
                _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 30);

                logger.LogDebug("Obtained Keycloak admin token, expires in {ExpiresIn}s", tokenResponse.ExpiresIn);

                return _cachedToken;
            }
            catch (HttpRequestException ex)
            {
                capturedException = ex;
                throw;
            }
            catch (JsonException ex)
            {
                capturedException = ex;
                throw;
            }
            finally
            {
                if (capturedException is not null)
                    activity?.SetStatus(ActivityStatusCode.Error, capturedException.Message);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }
}
