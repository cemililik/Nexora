namespace Nexora.Architecture.Tests;

/// <summary>
/// T-027 architecture guard: every <c>ContactGdprDeletedIntegrationEventHandler</c>
/// in <c>src/Modules/&lt;Module&gt;/Infrastructure/IntegrationEvents/</c>
/// MUST reference the shared <c>IGdprRenamedTableScanner</c> helper, so a
/// new module that handles the event cannot silently skip the renamed-
/// <c>_del_</c>-table redaction step (per ADR-0028's GDPR escape hatch).
/// </summary>
/// <remarks>
/// <para>
/// The check is intentionally a source-text grep rather than a reflective
/// runtime check: the helper is invoked through a callback pattern, so a
/// reflective check on dependencies wouldn't tell us whether the handler
/// actually <i>uses</i> the injected scanner. Reading the source for the
/// type name catches both "forgot to inject" and "injected but never
/// invoked" — exactly the regressions we want CI to fire on.
/// </para>
/// <para>
/// Modules whose handlers legitimately don't need the escape hatch (e.g.
/// Identity nullifies a FK column with no PII; Audit applies its own
/// redaction to JSON payload columns whose schema doesn't carry per-
/// contact PII) are listed in <see cref="ScanExemptModules"/> with a
/// short rationale. Adding to that list is a deliberate decision and
/// shows up in code review.
/// </para>
/// </remarks>
public sealed class GdprEscapeHatchBoundaryTests
{
    private const string HandlerFileName = "ContactGdprDeletedIntegrationEventHandler.cs";

    /// <summary>
    /// Modules whose handler does not need the renamed-table scanner.
    /// Each entry MUST carry a rationale that a reviewer can sanity-check.
    /// </summary>
    private static readonly Dictionary<string, string> ScanExemptModules =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["identity"] =
                "Identity handler nullifies a single FK column (User.ContactId) — no PII payload " +
                "lives in identity_*_del_* tables, so a renamed-table scan would target nothing.",
            ["audit"] =
                "Audit handler invokes RedactPayloadForGdpr() on AuditEntry rows that match the " +
                "ContactId. The renamed-table scan does NOT fit because audit retention rules " +
                "(ADR-0008) override the ADR-0028 retention window — purging renamed audit " +
                "tables would conflict with the audit module's own retention policy.",
        };

    [Fact]
    public void EveryNonExemptHandler_References_IGdprRenamedTableScanner()
    {
        var modulesDir = LocateModulesDir();
        var handlerPaths = Directory.EnumerateFiles(
                modulesDir, HandlerFileName, SearchOption.AllDirectories)
            // Filter to the standard
            // src/Modules/Nexora.Modules.<Mod>/Infrastructure/IntegrationEvents path.
            .Where(p => p.Contains($"{Path.DirectorySeparatorChar}Infrastructure{Path.DirectorySeparatorChar}IntegrationEvents{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        handlerPaths.Should().NotBeEmpty(
            "expected at least one ContactGdprDeletedIntegrationEventHandler under src/Modules/.");

        var offenders = new List<string>();
        foreach (var path in handlerPaths)
        {
            var moduleName = ExtractModuleName(path);
            if (ScanExemptModules.ContainsKey(moduleName)) continue;

            var src = File.ReadAllText(path);
            if (!src.Contains("IGdprRenamedTableScanner", StringComparison.Ordinal)
                || !src.Contains("ScanAsync", StringComparison.Ordinal))
            {
                offenders.Add(
                    $"  - {moduleName}: {path} does not reference IGdprRenamedTableScanner.ScanAsync. " +
                    "If the module legitimately doesn't need the scan, add it to ScanExemptModules with a rationale.");
            }
        }

        offenders.Should().BeEmpty(
            "every module that handles ContactGdprDeletedIntegrationEvent MUST also redact renamed " +
            "_del_ tables via IGdprRenamedTableScanner per ADR-0028 (T-027). Offenders:\n" +
            string.Join("\n", offenders));
    }

    private static string ExtractModuleName(string handlerPath)
    {
        // .../src/Modules/Nexora.Modules.<Module>/Infrastructure/IntegrationEvents/Handler.cs
        var parts = handlerPath.Split(Path.DirectorySeparatorChar);
        var modulesIdx = Array.IndexOf(parts, "Modules");
        if (modulesIdx < 0 || modulesIdx + 1 >= parts.Length)
            return "<unknown>";
        var projectFolder = parts[modulesIdx + 1]; // Nexora.Modules.<Module>
        const string prefix = "Nexora.Modules.";
        return projectFolder.StartsWith(prefix, StringComparison.Ordinal)
            ? projectFolder[prefix.Length..].ToLowerInvariant()
            : projectFolder.ToLowerInvariant();
    }

    private static string LocateModulesDir()
    {
        // Walk up from the test binary until a sibling src/Modules surfaces;
        // mirrors the discovery pattern used by sibling architecture tests
        // (e.g. AdditiveOnlyMigrationTests).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Modules");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate src/Modules directory from the test binary's BaseDirectory.");
    }
}
