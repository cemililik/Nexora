using Nexora.SharedKernel.Localization;

namespace Nexora.Modules.Identity.Infrastructure.Keycloak;

/// <summary>
/// Thrown when an unexpected failure occurs during Keycloak Admin API communication.
/// Carries a structured <see cref="LocalizedMessage"/> so the GlobalExceptionHandler
/// can return a translatable error key in the <c>ApiEnvelope</c>.
/// </summary>
public sealed class KeycloakIntegrationException : Exception
{
    public LocalizedMessage LocalizedMessage { get; }

    public KeycloakIntegrationException(string localizationKey, Dictionary<string, string>? meta = null, Exception? innerException = null)
        : base(localizationKey, innerException)
    {
        LocalizedMessage = LocalizedMessage.Of(localizationKey, meta);
    }
}
