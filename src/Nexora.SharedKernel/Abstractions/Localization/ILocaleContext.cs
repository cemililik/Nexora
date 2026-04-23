namespace Nexora.SharedKernel.Abstractions.Localization;

/// <summary>
/// Provides the resolved locale context for the current request.
/// Resolution order: User preference → Tenant default → Platform default.
/// <para>
/// - <see cref="Language"/> and <see cref="Timezone"/> may be overridden per-user.<br/>
/// - <see cref="Locale"/>, <see cref="Currency"/>, and <see cref="DocumentLanguage"/> are tenant-level
///   and control financial calculations, reports, and generated documents — not the UI display language.
/// </para>
/// </summary>
public interface ILocaleContext
{
    /// <summary>BCP 47 language tag for UI display (e.g. "en", "tr"). User → Tenant → "en".</summary>
    string Language { get; }

    /// <summary>IETF locale tag for number/date formatting (e.g. "en-US", "tr-TR"). Tenant → "en-US".</summary>
    string Locale { get; }

    /// <summary>ISO 4217 currency code (e.g. "USD", "TRY"). Tenant → "USD".</summary>
    string Currency { get; }

    /// <summary>IANA timezone identifier (e.g. "UTC", "Europe/Istanbul"). User → Tenant → "UTC".</summary>
    string Timezone { get; }

    /// <summary>BCP 47 language tag for generated documents (e.g. "en", "tr"). Tenant → "en".</summary>
    string DocumentLanguage { get; }
}
