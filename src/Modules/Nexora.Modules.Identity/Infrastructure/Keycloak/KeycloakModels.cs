using System.Text.Json.Serialization;

namespace Nexora.Modules.Identity.Infrastructure.Keycloak;

/// <summary>Token response from Keycloak's token endpoint.</summary>
internal sealed record KeycloakTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;
}

/// <summary>Realm representation for Keycloak Admin API.</summary>
internal sealed record KeycloakRealmRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("realm")]
    public string Realm { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }
}

/// <summary>User representation for Keycloak Admin API.</summary>
internal sealed record KeycloakUserRepresentation
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; init; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("emailVerified")]
    public bool EmailVerified { get; init; }

    [JsonPropertyName("credentials")]
    public List<KeycloakCredential>? Credentials { get; init; }
}

/// <summary>Credential representation for setting user passwords.</summary>
internal sealed record KeycloakCredential
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "password";

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("temporary")]
    public bool Temporary { get; init; } = true;
}
