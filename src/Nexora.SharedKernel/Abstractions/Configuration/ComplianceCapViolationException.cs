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
/// <para>
/// The English diagnostic sentence in <see cref="Exception.Message"/> is deliberately
/// NOT derived from the <see cref="LocalizationKey"/>: logs and stack traces should
/// carry a human-readable summary; the lockey is for user-facing translation only.
/// </para>
/// </remarks>
public sealed class ComplianceCapViolationException : Exception
{
    /// <summary>Initialises the exception with full cap metadata.</summary>
    /// <param name="key">The configuration key that was rejected (e.g. <c>gdpr.hard_delete.enabled</c>).</param>
    /// <param name="lockey">Localization key for the user-facing message.</param>
    /// <param name="isForced">Whether the blocking cap was <c>Forced</c>.</param>
    /// <param name="forcedValue">Serialized forced value when <paramref name="isForced"/> is <see langword="true"/>.</param>
    /// <param name="allowed">Whether the cap permits the key to be set at all.</param>
    public ComplianceCapViolationException(
        string key,
        string lockey,
        bool isForced,
        string? forcedValue,
        bool allowed)
        : base($"Compliance cap blocks override for '{key}' (allowed={allowed}, forced={isForced}).")
    {
        Key = key;
        LocalizationKey = lockey;
        IsForced = isForced;
        ForcedValue = forcedValue;
        Allowed = allowed;
    }

    /// <summary>Configuration key that was rejected.</summary>
    public string Key { get; }

    /// <summary>Localization key describing the specific cap violation.</summary>
    public string LocalizationKey { get; }

    /// <summary>
    /// <see langword="true"/> when the cap mandates a specific value that the override
    /// contradicted (<c>cap.Forced</c>).
    /// </summary>
    public bool IsForced { get; }

    /// <summary>
    /// Serialized value the cap is forcing — non-null only when <see cref="IsForced"/>
    /// is <see langword="true"/>.
    /// </summary>
    public string? ForcedValue { get; }

    /// <summary>
    /// <see langword="false"/> when the cap forbids any override for this key at all.
    /// </summary>
    public bool Allowed { get; }
}
