using System.Text.Json;
using Nexora.Host.Cli;

namespace Nexora.Host.Tests.Cli;

/// <summary>
/// Parity test for the CLI lockey bundles (T-006 review follow-up).
/// Asserts:
///   1. Both <c>en</c> and <c>tr</c> bundles loaded from embedded JSON
///      contain the same set of keys.
///   2. Every key referenced by code via <see cref="CliLocalization.T"/> exists
///      in both bundles (so a missing translation never silently falls back
///      to the lockey itself in production).
///   3. The on-disk JSON files at <c>src/Nexora.Host/Cli/Locales/host.{en,tr}.json</c>
///      match the embedded resources byte-for-byte after key/value
///      normalisation — translators can edit either copy with confidence
///      that the build embeds what they wrote.
/// </summary>
public sealed class CliLocalizationParityTests
{
    /// <summary>
    /// Every lockey referenced by name in the CLI codepath. Kept as a
    /// hand-maintained list (not reflection-scanned) so a deliberately-removed
    /// lockey requires updating this test in the same PR — the mismatch
    /// surfaces the change.
    /// </summary>
    private static readonly string[] CodeReferencedLockeys =
    [
        // Usage / help (CliDispatcher.PrintUsage)
        "lockey_cli_usage_title",
        "lockey_cli_usage_commands_header",
        "lockey_cli_usage_demoload_signature",
        "lockey_cli_usage_demoload_description_l1",
        "lockey_cli_usage_demoload_description_l2",
        "lockey_cli_usage_demoload_description_l3",
        "lockey_cli_usage_exitcodes_header",
        "lockey_cli_usage_exitcode_success",
        "lockey_cli_usage_exitcode_usage_template",
        "lockey_cli_usage_exitcode_partial_template",

        // demo:load (DemoLoadCommand.Run + RunAsync + WriteFailure)
        "lockey_cli_demoload_cancelled",
        "lockey_cli_demoload_failed_generic",
        "lockey_cli_demoload_failed_verbose_template",
        "lockey_cli_demoload_usage_hint",
        "lockey_cli_demoload_tenant_schema_missing_template",
        "lockey_cli_demoload_tenant_schema_missing_pointer",
        "lockey_cli_demoload_dryrun_header_template",
        "lockey_cli_demoload_dryrun_plan_template",
        "lockey_cli_demoload_dryrun_no_changes",
        "lockey_cli_demoload_completed_template",
        "lockey_cli_demoload_outcome_seeded",
        "lockey_cli_demoload_outcome_already_seeded",
        "lockey_cli_demoload_outcome_noop",
        "lockey_cli_demoload_outcome_failed_template",
        "lockey_cli_demoload_outcome_line_template",
    ];

    [Fact]
    public void Bundles_HaveIdenticalKeySets_AcrossLocales()
    {
        var bundles = CliLocalization.Bundles;
        bundles.Should().ContainKey("en");
        bundles.Should().ContainKey("tr");

        var enKeys = bundles["en"].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var trKeys = bundles["tr"].Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        trKeys.Should().BeEquivalentTo(enKeys,
            "tr bundle MUST cover every key the en bundle does — otherwise a Turkish-locale operator silently sees the English fallback (or worse, the raw lockey if en lacks it).");
    }

    [Fact]
    public void EveryCodeReferencedLockey_ExistsInBothLocales()
    {
        var bundles = CliLocalization.Bundles;
        var missing = new List<string>();

        foreach (var lockey in CodeReferencedLockeys)
        {
            if (!bundles["en"].ContainsKey(lockey))
                missing.Add($"en: {lockey}");
            if (!bundles["tr"].ContainsKey(lockey))
                missing.Add($"tr: {lockey}");
        }

        missing.Should().BeEmpty(
            "every lockey referenced by name in the CLI must exist in both bundles. Missing entries:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void EmbeddedBundles_MatchOnDiskJsonFiles()
    {
        // Source of truth: src/Nexora.Host/Cli/Locales/host.{en,tr}.json
        // Translators edit those files; the build embeds them. This test
        // makes sure the embed step actually shipped what's on disk.
        var repoRoot = FindRepoRoot();
        AssertOnDiskMatchesEmbedded(repoRoot, "host.en.json", "en");
        AssertOnDiskMatchesEmbedded(repoRoot, "host.tr.json", "tr");
    }

    private static void AssertOnDiskMatchesEmbedded(
        string repoRoot, string fileName, string locale)
    {
        var path = Path.Combine(
            repoRoot, "src", "Nexora.Host", "Cli", "Locales", fileName);
        File.Exists(path).Should().BeTrue(
            $"on-disk source-of-truth file '{path}' must exist — translators edit it directly.");

        using var fs = File.OpenRead(path);
        var onDisk = JsonSerializer.Deserialize<Dictionary<string, string>>(fs)!;
        var embedded = CliLocalization.Bundles[locale];

        onDisk.Should().HaveCount(embedded.Count,
            $"{fileName} entry count must match the embedded {locale} bundle.");
        foreach (var (key, value) in onDisk)
        {
            embedded.Should().ContainKey(key,
                $"on-disk key '{key}' missing from embedded bundle — re-build to refresh the resource.");
            embedded[key].Should().Be(value,
                $"value for key '{key}' diverges between {fileName} and the embedded bundle.");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Nexora.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
