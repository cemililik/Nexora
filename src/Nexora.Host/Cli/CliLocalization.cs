using System.Globalization;
using System.Text.Json;

namespace Nexora.Host.Cli;

/// <summary>
/// Pre-DI lockey resolver for CLI text (T-006 review follow-up).
///
/// <para>
/// The CLI runs BEFORE the host's DI container exists, so it cannot resolve
/// <c>ILocalizationService</c>; the runtime translation pipeline is therefore
/// off-limits at this seam. To keep the project's "no hardcoded user-facing
/// strings" invariant, every string the CLI prints maps to a <c>lockey_</c>
/// key here, and this class returns the localized fallback for the current
/// process locale.
/// </para>
/// <para>
/// <b>Source of truth:</b> the canonical strings live in two JSON files —
/// <c>Cli/Locales/host.en.json</c> and <c>Cli/Locales/host.tr.json</c> —
/// shipped next to the host binary via the <c>Content</c> entries in
/// <c>Nexora.Host.csproj</c>. Translators edit those files directly; the
/// build copies them into the output and publish directories.
/// <c>CliLocalizationParityTests</c> (Nexora.Host.Tests) asserts the in-memory
/// map matches every code-referenced lockey in both locales.
/// </para>
/// <para>
/// Loading happens once at first use via <see cref="AppContext.BaseDirectory"/>.
/// Embedded-resource loading was attempted first but Microsoft.NET.Sdk.Web
/// silently drops <c>EmbeddedResource</c> entries for <c>*.json</c> paths
/// under arbitrary subfolders; the disk-based approach sidesteps that quirk
/// and has the side-benefit of letting an operator hot-edit a locale on
/// a deployed host without re-publishing.
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
    /// Path within the binary output where the locale JSONs land — see the
    /// <c>Content Include</c> entries in <c>Nexora.Host.csproj</c>. Stored
    /// as a static readonly (not a private const) so the naming rule treats
    /// it as a field and the underscore-prefix convention applies — the
    /// project linter does not differentiate const from field.
    /// </summary>
    private static readonly string _localesSubdirectory = "Cli/Locales";

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> _bundles =
        new(LoadBundles, isThreadSafe: true);

    /// <summary>
    /// Resolve <paramref name="lockey"/> for the current culture. Falls back
    /// to <c>en</c> when the culture has no entry, and to the lockey itself
    /// when neither has one (so a missing translation surfaces as the key —
    /// loud, not silent).
    /// </summary>
    public static string T(string lockey)
    {
        var bundles = _bundles.Value;
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (bundles.TryGetValue(culture, out var bundle) &&
            bundle.TryGetValue(lockey, out var translated))
        {
            return translated;
        }
        if (bundles.TryGetValue("en", out var en) &&
            en.TryGetValue(lockey, out var enText))
        {
            return enText;
        }
        return lockey;
    }

    /// <summary>
    /// Read-only view of the loaded translations — used by the parity test
    /// to assert every code-referenced lockey is present in both locales.
    /// Internal so it doesn't bleed into the public CLI surface.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Bundles
        => _bundles.Value;

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadBundles()
    {
        var localesDir = Path.Combine(AppContext.BaseDirectory, _localesSubdirectory);
        var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = ReadFromDisk(Path.Combine(localesDir, "host.en.json")),
            ["tr"] = ReadFromDisk(Path.Combine(localesDir, "host.tr.json")),
        };
        return dict;
    }

    private static IReadOnlyDictionary<string, string> ReadFromDisk(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"CLI locale file '{path}' is missing. Confirm the Content entry in Nexora.Host.csproj " +
                "(Cli/Locales/host.{en,tr}.json with CopyToOutputDirectory=PreserveNewest) and that the " +
                "build copied the JSON into the output directory.");
        }
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException(
                $"CLI locale file '{path}' deserialized to null — file is empty or malformed.");
    }
}
