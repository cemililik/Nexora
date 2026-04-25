using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Licensing;

/// <summary>
/// T-014: bound to <c>Licensing:Reload</c>. Controls the polling-loop
/// hot-reload service per ADR-0030 (chosen mechanism). Values are
/// validated at startup via <see cref="IValidateOptions{T}"/>.
/// </summary>
public sealed class LicenseReloadOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Licensing:Reload";

    /// <summary>Path to the license file watched for changes. Default <c>/var/lib/nexora/license.lic</c>.</summary>
    [Required]
    public string LicenseFilePath { get; set; } = "/var/lib/nexora/license.lic";

    /// <summary>
    /// Polling interval in seconds. Default 30 — operator-tolerable for
    /// a license flip. Range 5–600 enforced by <see cref="LicenseReloadOptionsValidator"/>
    /// per ADR-0030.
    /// </summary>
    [Range(5, 600)]
    public int PollingIntervalSeconds { get; set; } = 30;
}

/// <summary>Boots-time validation enforcing the ADR-0030 polling-interval range.</summary>
public sealed class LicenseReloadOptionsValidator : IValidateOptions<LicenseReloadOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LicenseReloadOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LicenseFilePath))
            return ValidateOptionsResult.Fail($"{nameof(options.LicenseFilePath)} is required.");

        if (options.PollingIntervalSeconds is < 5 or > 600)
            return ValidateOptionsResult.Fail(
                $"{nameof(options.PollingIntervalSeconds)} must be in [5, 600] (ADR-0030).");

        return ValidateOptionsResult.Success;
    }
}
