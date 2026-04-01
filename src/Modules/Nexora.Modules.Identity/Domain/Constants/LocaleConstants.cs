namespace Nexora.Modules.Identity.Domain.Constants;

/// <summary>Allowed values for tenant and user locale settings.</summary>
public static class LocaleConstants
{
    /// <summary>IETF locale tags supported by the platform.</summary>
    public static readonly IReadOnlyList<string> SupportedLocales =
    [
        "en-US",
        "tr-TR",
    ];

    /// <summary>ISO 4217 currency codes supported by the platform.</summary>
    public static readonly IReadOnlyList<string> SupportedCurrencies =
    [
        "USD",
        "EUR",
        "TRY",
    ];

    /// <summary>IANA timezone identifiers supported by the platform.</summary>
    public static readonly IReadOnlyList<string> SupportedTimezones =
    [
        "UTC",
        "Europe/Istanbul",
        "America/New_York",
        "Europe/London",
        "Asia/Dubai",
        "Asia/Tokyo",
        "Australia/Sydney",
    ];

    /// <summary>BCP 47 language tags supported by the platform.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages =
    [
        "en",
        "tr",
    ];
}
