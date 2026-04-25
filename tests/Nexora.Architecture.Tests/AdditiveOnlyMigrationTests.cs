using System.Text.RegularExpressions;
using Nexora.Architecture.Tests.Helpers;

namespace Nexora.Architecture.Tests;

/// <summary>
/// T-012: enforces the additive-only migration rule from ADR-0003 +
/// `docs/operations/migration-orchestration.md` §1.2 mechanically — every
/// EF Core migration's <c>Up</c> method must avoid destructive operations
/// (<c>DropColumn</c>, <c>RenameColumn</c>, narrowing <c>AlterColumn</c>).
/// Without this guard, regressions would only be caught in code review;
/// once a destructive migration ships to production, the only recoveries
/// are tenant-by-tenant data fix-ups and manual schema repairs.
///
/// <para>
/// <b>Scope.</b> Scans every <c>src/**/Migrations/*.cs</c> file (EF
/// Core's generated location convention) for the forbidden tokens. Phase
/// 1.5 modules ship via <c>DevelopmentSeed.ApplySchemaUpdatesAsync</c>
/// rather than EF migrations, so the test will currently match zero
/// files — that is the correct behaviour. As Phase 2 modules add their
/// first EF migrations the test starts enforcing automatically; no
/// per-module configuration needed.
/// </para>
///
/// <para>
/// <b>Allowlist.</b> A migration that legitimately needs a destructive
/// operation (e.g. a corrupted-column purge after an incident) MUST
/// carry the comment marker
/// <c>// architecture-allow: destructive-migration ADR-NNNN</c> on a
/// line within the same migration file. The marker requires a real ADR
/// reference — the four-digit number is enforced — so the destructive
/// change is documented and reviewable. Without an ADR, the marker
/// fails the test.
/// </para>
/// </summary>
public sealed class AdditiveOnlyMigrationTests
{
    private static readonly string RepoSrcRoot = FindRepoSrcRoot();

    private static string FindRepoSrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Nexora.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return Path.Combine(dir.FullName, "src");
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repository root starting from " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// Per-rule [Theory] entry: each forbidden EF Core
    /// <c>migrationBuilder.X(...)</c> call gets its own scan + assertion
    /// so a CI failure pinpoints the exact rule that tripped instead of
    /// burying the cause inside an aggregated "any" assertion.
    /// </summary>
    [Theory]
    [InlineData("DropColumn", @"\bmigrationBuilder\s*\.\s*DropColumn\s*\(",
        "DROP COLUMN destroys data permanently. ADR-0003: production migrations are additive. " +
        "Either keep the column and stop writing to it, OR write a separate ADR documenting the destructive intent " +
        "and add an `// architecture-allow: destructive-migration ADR-NNNN` marker on the migration line.")]
    [InlineData("DropTable", @"\bmigrationBuilder\s*\.\s*DropTable\s*\(",
        "DROP TABLE destroys data permanently. Soft-rename via T-026's uninstall path or a dedicated ADR. " +
        "If truly intentional, add `// architecture-allow: destructive-migration ADR-NNNN`.")]
    [InlineData("RenameColumn", @"\bmigrationBuilder\s*\.\s*RenameColumn\s*\(",
        "RENAME COLUMN breaks rolling deploys (old code reads the old name during the deploy window). " +
        "ADR-0003: prefer add-new + dual-write + drop-old across two releases. " +
        "Marker: `// architecture-allow: destructive-migration ADR-NNNN` if the deploy plan handles this.")]
    [InlineData("RenameTable", @"\bmigrationBuilder\s*\.\s*RenameTable\s*\(",
        "RENAME TABLE breaks rolling deploys for the same reason as RenameColumn. " +
        "Marker: `// architecture-allow: destructive-migration ADR-NNNN`.")]
    public void Migration_UpMethod_DoesNotCallDestructiveOperation(
        string operation, string pattern, string remediation)
    {
        var offenders = ScanMigrationsForPattern(pattern, operation, remediation);
        offenders.Should().BeEmpty(
            $"no migration may call `migrationBuilder.{operation}(…)` from its Up method without an ADR-backed allowlist marker. " +
            $"Offending occurrences:\n{string.Join("\n", offenders)}");
    }

    /// <summary>
    /// AlterColumn is allowed in additive cases (widening, default change,
    /// nullability relaxation) but is forbidden when narrowing — making
    /// a column nullable→non-nullable, shrinking a string length, or
    /// changing the type to a smaller domain. The static analyser cannot
    /// reliably distinguish all narrowing cases without parsing the type
    /// args, so this test fires on EVERY <c>AlterColumn</c> call and
    /// requires an explicit allowlist marker per ADR-0003 §"AlterColumn
    /// review gate" — the marker forces a human review of each change.
    /// </summary>
    [Fact]
    public void Migration_UpMethod_AlterColumnRequiresAdrMarker()
    {
        var pattern = @"\bmigrationBuilder\s*\.\s*AlterColumn\s*<";
        var offenders = ScanMigrationsForPattern(
            pattern,
            "AlterColumn",
            "AlterColumn cannot be statically classified as additive — every call must be reviewed for " +
            "nullability tightening, length shrinking, or type narrowing per ADR-0003. " +
            "Add `// architecture-allow: destructive-migration ADR-NNNN` on the migration after a maintainer " +
            "verifies the change is widening / default-change-only / nullability-relaxation.");
        offenders.Should().BeEmpty(
            "every AlterColumn call must carry an ADR-backed allowlist marker. " +
            $"Offending occurrences:\n{string.Join("\n", offenders)}");
    }

    /// <summary>
    /// Self-test: this scanner exists to FIRE on real drift. If a future
    /// refactor breaks the regex / file enumeration / allowlist parsing,
    /// every other test in this class would silently pass. The self-test
    /// constructs a synthetic destructive migration in a temp directory
    /// AND a synthetic allowlisted one, then runs the scanner against
    /// JUST that directory and asserts both branches behave correctly.
    /// </summary>
    [Fact]
    public void Scanner_OnContrivedFiles_DetectsDestructiveAndHonoursAllowlist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"nexora-adr-arch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var migrationsDir = Path.Combine(tempDir, "Migrations");
            Directory.CreateDirectory(migrationsDir);

            // 1) destructive without marker — must be detected
            File.WriteAllText(
                Path.Combine(migrationsDir, "20260101000000_BadDrop.cs"),
                """
                public partial class BadDrop : Migration {
                    protected override void Up(MigrationBuilder migrationBuilder) {
                        migrationBuilder.DropColumn(name: "Email", table: "users");
                    }
                }
                """);

            // 2) destructive WITH marker — must be honoured
            File.WriteAllText(
                Path.Combine(migrationsDir, "20260102000000_AllowedDrop.cs"),
                """
                public partial class AllowedDrop : Migration {
                    // architecture-allow: destructive-migration ADR-9999
                    protected override void Up(MigrationBuilder migrationBuilder) {
                        migrationBuilder.DropColumn(name: "Legacy", table: "users");
                    }
                }
                """);

            var pattern = @"\bmigrationBuilder\s*\.\s*DropColumn\s*\(";
            var offenders = ScanFilesForPattern(
                Directory.EnumerateFiles(migrationsDir, "*.cs", SearchOption.AllDirectories),
                pattern,
                "DropColumn",
                "self-test remediation");

            offenders.Should().HaveCount(1,
                "exactly the unmarked file must be flagged; the allowlisted one must not.");
            offenders[0].Should().Contain("BadDrop.cs");
            offenders[0].Should().NotContain("AllowedDrop.cs");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // --- Scanner internals ---------------------------------------------------

    /// <summary>
    /// Marker that exempts a migration file from a destructive-operation
    /// rule. Requires a four-digit ADR reference so the destructive
    /// change is paired with documented decision-record reasoning.
    /// </summary>
    private static readonly Regex AllowlistMarker = new(
        @"//\s*architecture-allow:\s*destructive-migration\s+ADR-\d{4}",
        RegexOptions.Compiled);

    private static IReadOnlyList<string> ScanMigrationsForPattern(
        string pattern, string operation, string remediation)
    {
        // EF Core convention: migrations land under `Migrations/` folders
        // beside the DbContext. Scan recursively under src/ so module
        // additions are picked up automatically.
        var migrationFiles = Directory.EnumerateDirectories(RepoSrcRoot, "Migrations", SearchOption.AllDirectories)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            // Skip designer files (EF Core generates *.Designer.cs that
            // contains the snapshot of the model — destructive operations
            // there are model-state diff bookkeeping, not actual SQL).
            .Where(p => !p.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));

        return ScanFilesForPattern(migrationFiles, pattern, operation, remediation);
    }

    private static IReadOnlyList<string> ScanFilesForPattern(
        IEnumerable<string> files, string pattern, string operation, string remediation)
    {
        var rx = new Regex(pattern, RegexOptions.Compiled);
        var hits = new List<string>();
        foreach (var file in files)
        {
            // Strip comments + string literals before matching so the
            // pattern cannot false-hit on a doc comment that mentions
            // `migrationBuilder.DropColumn(...)` in prose. Same shared
            // helper as NexoraJobBoundaryTests + PermissionRegistryBoundaryTests.
            var raw = File.ReadAllText(file);
            var stripped = SourceTextStripper.StripCommentsAndStrings(raw);
            if (!rx.IsMatch(stripped)) continue;

            // Allowlist is sourced from the ORIGINAL text (not stripped),
            // because the marker IS a comment.
            if (AllowlistMarker.IsMatch(raw))
            {
                continue;
            }

            // Find the first matching line for an operator-friendly
            // pointer. Match against the stripped text so the line number
            // points at the call site, not at a comment hit.
            var lineNumber = FindFirstMatchLine(stripped, rx);
            var rel = Path.GetRelativePath(RepoSrcRoot, file);
            hits.Add($"  - {rel}:{lineNumber} — `{operation}` call. {remediation}");
        }
        return hits;
    }

    private static int FindFirstMatchLine(string text, Regex rx)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (rx.IsMatch(lines[i])) return i + 1;
        }
        return 0;
    }
}
