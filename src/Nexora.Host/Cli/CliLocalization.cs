using System.Globalization;

namespace Nexora.Host.Cli;

/// <summary>
/// Pre-DI lockey resolver for CLI text (T-006 review follow-up).
///
/// <para>
/// The CLI runs BEFORE the host's DI container exists, so it cannot resolve
/// <c>ILocalizationService</c>; the runtime translation pipeline is therefore
/// off-limits at this seam. To keep the project's "no hardcoded user-facing
/// strings" invariant, every string the CLI prints maps to a <c>lockey_</c>
/// key here, and this class returns the English fallback for the current
/// process locale. Translations for <c>tr</c> (and any future locale) live in
/// <see cref="Translations"/> as in-process dictionaries — they are NOT
/// extracted from the runtime locale store because the runtime store is
/// itself unavailable at this seam.
/// </para>
/// <para>
/// Runtime DI consumers continue to use <c>ILocalizationService</c> /
/// <c>LocalizedMessage</c> as normal; this class is the narrow boundary for
/// the pre-DI window only.
/// </para>
/// </summary>
internal static class CliLocalization
{
    /// <summary>
    /// Resolve <paramref name="lockey"/> for the current culture. Falls back
    /// to <c>en</c> when the culture has no entry, and to the lockey itself
    /// when neither has one (so a missing translation surfaces as the key).
    /// </summary>
    public static string T(string lockey)
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (Translations.TryGetValue(culture, out var bundle) &&
            bundle.TryGetValue(lockey, out var translated))
        {
            return translated;
        }
        if (Translations.TryGetValue("en", out var en) &&
            en.TryGetValue(lockey, out var enText))
        {
            return enText;
        }
        return lockey;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["lockey_cli_usage_title"] = "Nexora host CLI",
            ["lockey_cli_usage_commands_header"] = "Commands:",
            ["lockey_cli_usage_demoload_signature"] = "  demo:load --tenant=<guid> --scenario=<name> [--dry-run]",
            ["lockey_cli_usage_demoload_description_l1"] = "      Runs IDemoDataSeeder for the given tenant+scenario. Requires",
            ["lockey_cli_usage_demoload_description_l2"] = "      the tenant schema to already exist — tenant provisioning is a",
            ["lockey_cli_usage_demoload_description_l3"] = "      separate admin API (see docs/roadmap/phases/phase-1.5-bridge.md).",
            ["lockey_cli_usage_exitcodes_header"] = "Exit codes:",
            ["lockey_cli_usage_exitcode_success"] = "  0  success (or all modules already seeded)",
            ["lockey_cli_usage_exitcode_usage_template"] = "  {0}  usage / validation error",
            ["lockey_cli_usage_exitcode_partial_template"] = "  {0}  one or more modules failed during seeding",
            ["lockey_cli_demoload_cancelled"] = "demo:load cancelled.",
        },
        ["tr"] = new Dictionary<string, string>
        {
            ["lockey_cli_usage_title"] = "Nexora host CLI",
            ["lockey_cli_usage_commands_header"] = "Komutlar:",
            ["lockey_cli_usage_demoload_signature"] = "  demo:load --tenant=<guid> --scenario=<name> [--dry-run]",
            ["lockey_cli_usage_demoload_description_l1"] = "      Verilen tenant+senaryo için IDemoDataSeeder çalıştırır. Tenant",
            ["lockey_cli_usage_demoload_description_l2"] = "      şemasının zaten mevcut olması gerekir — tenant sağlama ayrı bir",
            ["lockey_cli_usage_demoload_description_l3"] = "      admin API'sidir (bkz. docs/roadmap/phases/phase-1.5-bridge.md).",
            ["lockey_cli_usage_exitcodes_header"] = "Çıkış kodları:",
            ["lockey_cli_usage_exitcode_success"] = "  0  başarı (veya tüm modüller zaten seed edilmiş)",
            ["lockey_cli_usage_exitcode_usage_template"] = "  {0}  kullanım / doğrulama hatası",
            ["lockey_cli_usage_exitcode_partial_template"] = "  {0}  bir veya daha fazla modül seed sırasında başarısız oldu",
            ["lockey_cli_demoload_cancelled"] = "demo:load iptal edildi.",
        },
    };
}
