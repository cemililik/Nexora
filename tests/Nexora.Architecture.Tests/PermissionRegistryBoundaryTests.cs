using System.Text.RegularExpressions;

namespace Nexora.Architecture.Tests;

/// <summary>
/// Enforces T-020 / ADR-004 / permissions.md §3 boundary: permission declarations
/// (<c>Permission.Create(...)</c>) live in exactly one place — each module's
/// <c>OnStartupAsync</c> calls <c>IPermissionRegistry.Register(...)</c>; the DB-writing
/// side lives in <c>IdentityModuleMigration</c> and constructs <see cref="object"/>
/// instances from registry data, not hand-rolled constants.
/// </summary>
public sealed class PermissionRegistryBoundaryTests
{
    private static readonly string RepoSrcRoot = FindRepoSrcRoot();

    /// <summary>
    /// Walks upwards from the test assembly directory until a sentinel identifies the
    /// repository root (<c>Nexora.sln</c> or <c>.git</c>). Throws with a clear message
    /// when nothing is found so the test fails loudly instead of pointing at the wrong
    /// folder (which would let the regex scans silently pass on empty content).
    /// </summary>
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
            "Could not locate repository root (no Nexora.sln or .git ancestor found) starting from " +
            AppContext.BaseDirectory);
    }

    /// <summary>
    /// Strips <c>// line</c> and <c>/* block */</c> comments while preserving string
    /// literals. Used by the IdentityModuleMigration scan, which must still see string
    /// tokens ("contacts", "documents", …) as potential violations.
    /// </summary>
    private static string StripComments(string source)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(i + 2, source.Length);
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Roslyn-lite stripper: removes <c>// line</c> and <c>/* block */</c> comments plus
    /// string literals (<c>"…"</c>, <c>@"…"</c>) so regex scans only see executable code. Without this, a commented-out example
    /// or a string containing <c>Permission.Create(</c> would mask a genuine violation as
    /// a false positive OR a false negative — either way, the boundary test lies.
    /// Not a real parser; adequate for our grep-style guards against a single well-known
    /// call shape and a handful of module-name string constants.
    /// </summary>
    private static string StripCommentsAndStrings(string source)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            char next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') i++;
                continue;
            }
            if (c == '/' && next == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                i = Math.Min(i + 2, source.Length);
                continue;
            }
            // Verbatim string literal @"..." — "" is an escaped quote.
            if (c == '@' && next == '"')
            {
                i += 2;
                while (i < source.Length)
                {
                    if (source[i] == '"' && i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                    if (source[i] == '"') { i++; break; }
                    i++;
                }
                continue;
            }
            // Regular string literal "..." — \" is an escape.
            if (c == '"')
            {
                i++;
                while (i < source.Length)
                {
                    if (source[i] == '\\' && i + 1 < source.Length) { i += 2; continue; }
                    if (source[i] == '"') { i++; break; }
                    if (source[i] == '\n') break;
                    i++;
                }
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    [Fact]
    public void DevelopmentSeed_HardcodedPermissionCreate_ShouldNotExist()
    {
        var path = Path.Combine(RepoSrcRoot, "Nexora.Host", "DevelopmentSeed.cs");
        File.Exists(path).Should().BeTrue("DevelopmentSeed.cs must exist");

        var content = StripCommentsAndStrings(File.ReadAllText(path));
        var hits = Regex.Matches(content, @"Permission\.Create\s*\(");

        hits.Count.Should().Be(0,
            "DevelopmentSeed.cs must not hardcode permission declarations — they belong in each " +
            "module's IModule.OnStartupAsync via IPermissionRegistry.Register(...). See ADR-004.");
    }

    [Fact]
    public void IdentityModuleMigration_CrossModulePermissionCreate_ShouldNotExist()
    {
        var path = Path.Combine(
            RepoSrcRoot, "Modules", "Nexora.Modules.Identity",
            "Infrastructure", "IdentityModuleMigration.cs");
        File.Exists(path).Should().BeTrue("IdentityModuleMigration.cs must exist");

        // NOTE: we intentionally do NOT strip strings here — the whole point of this test
        // is to catch hardcoded module string literals. Comments are stripped so a
        // historical reference in a doc-comment doesn't flag.
        var content = StripComments(File.ReadAllText(path));

        // Hardcoded module strings besides "identity"/"platform" (Identity owns both) are
        // evidence of the pre-T-020 central seed list having been smuggled back in.
        var forbiddenModules = new[] { "\"contacts\"", "\"documents\"", "\"notifications\"",
            "\"reporting\"", "\"audit\"", "\"crm\"", "\"finance\"", "\"subscription\"" };

        var offenders = forbiddenModules
            .Where(m => content.Contains(m, StringComparison.Ordinal))
            .ToArray();

        offenders.Should().BeEmpty(
            "IdentityModuleMigration must read cross-module permissions from the registry, " +
            "not hardcode them. Offending module tokens: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DevelopmentSeed_CreateDefaultPermissionsMethod_ShouldNotExist()
    {
        var path = Path.Combine(RepoSrcRoot, "Nexora.Host", "DevelopmentSeed.cs");
        File.Exists(path).Should().BeTrue("DevelopmentSeed.cs must exist for this assertion");

        var content = StripCommentsAndStrings(File.ReadAllText(path));
        content.Should().NotMatchRegex(
            @"private\s+static\s+Permission\[\]\s+CreateDefaultPermissions",
            because: "DevelopmentSeed.CreateDefaultPermissions() was removed by T-020; " +
                     "permissions flow from the registry via IdentityModuleMigration.");
    }
}
