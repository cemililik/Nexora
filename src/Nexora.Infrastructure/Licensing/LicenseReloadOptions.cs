using System.ComponentModel.DataAnnotations;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-014: bound to <c>Licensing:Reload</c>. Controls the polling-loop
/// hot-reload service per ADR-0030 (chosen mechanism). Values are
/// validated at startup via <c>ValidateDataAnnotations().ValidateOnStart()</c>
/// — the <see cref="RequiredAttribute"/> + <see cref="RangeAttribute"/>
/// declarations on the properties below are the single source of truth.
/// </summary>
/// <remarks>
/// An earlier revision shipped both DataAnnotations and a custom
/// <c>IValidateOptions&lt;LicenseReloadOptions&gt;</c> implementation
/// covering the same checks. The duplication created two places to keep
/// in sync; the custom validator was removed in favour of the
/// declarative annotations + <c>ValidateOnStart()</c> at registration
/// time. If a future rule needs cross-property logic that DataAnnotations
/// cannot express, prefer adding a custom validator at that point and
/// remove the corresponding annotations rather than reintroducing the
/// duplication.
/// </remarks>
public sealed class LicenseReloadOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Licensing:Reload";

    /// <summary>Path to the license file watched for changes. Default <c>/var/lib/nexora/license.lic</c>.</summary>
    [Required]
    public string LicenseFilePath { get; set; } = "/var/lib/nexora/license.lic";

    /// <summary>
    /// Polling interval in seconds. Default 30 — operator-tolerable for
    /// a license flip. Range 5–600 enforced by the declarative
    /// <see cref="RangeAttribute"/>.
    /// </summary>
    [Range(5, 600)]
    public int PollingIntervalSeconds { get; set; } = 30;
}
