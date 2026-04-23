namespace Nexora.SharedKernel.Abstractions.Configuration;

/// <summary>
/// Well-known keys consumed by <see cref="IConfigurationResolver"/>. Keeps the
/// string constants in one place so module code does not sprinkle magic strings.
/// </summary>
public static class ComplianceKey
{
    /// <summary>
    /// Feature flag: when <c>true</c>, GDPR erasure hard-deletes the contact and its child
    /// entities; when <c>false</c> (default), the contact is anonymized in-place.
    /// Consumed by <c>RequestGdprDeleteHandler</c>.
    /// </summary>
    public const string GdprHardDeleteEnabled = "gdpr.hard_delete.enabled";
}
