namespace Nexora.SharedKernel.Constants;

/// <summary>
/// Canonical string used to replace personally identifying fields during GDPR/KVKK
/// erasure and anonymization. All modules MUST use this constant — never hardcode
/// "[REDACTED]", "REDACTED", or similar placeholders.
/// </summary>
public static class PiiRedactedPlaceholder
{
    /// <summary>The canonical placeholder value.</summary>
    public const string Value = "[REDACTED]";
}
