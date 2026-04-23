namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Thrown by <see cref="IConfigurationResolver.SetOrgOverrideAsync{T}"/> when the
/// platform cap for the key does not permit the requested override (either
/// <c>cap.Allowed = false</c>, or <c>cap.Forced = true</c> and the override
/// contradicts the forced value).
/// </summary>
/// <remarks>
/// This is an expected failure mode at the API boundary — endpoint code converts it
/// to a localized <c>ApiEnvelope</c> failure rather than surfacing it as a 500. See
/// ADR-0025 §Implementation notes.
/// </remarks>
public sealed class ComplianceCapViolationException(string key, string lockey)
    : Exception($"Compliance cap blocks override for '{key}' ({lockey}).")
{
    /// <summary>Configuration key that was rejected.</summary>
    public string Key { get; } = key;

    /// <summary>Localization key describing the specific cap violation.</summary>
    public string LocalizationKey { get; } = lockey;
}
